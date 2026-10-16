// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VR_EditableElement.cs
//  ---------------------------------------------------------------------------
//  Socle commun aux fonctionnalites VR-FP1-2 / VR-FP1-3 / VR-FP1-4 / VR-FP1-5.
//
//  Ce composant est pose sur chaque prefab d'element editable (plateforme,
//  piege, ...). Il stocke les donnees ecrites dans le JSON d'export (VR-FP1-5) :
//      * "type"                   -> elementType
//      * position / rotation / scale -> lus directement sur le Transform
//      * "moveParams"             -> hasMovement / moveFrom / moveTo / duration
//
//  Il fournit egalement :
//      * la surbrillance de l'element selectionne (VR-FP1-2 et VR-FP1-3)
//      * l'etat "verrouille" (un element verrouille ne peut pas etre supprime)
//      * le deplacement automatique des plateformes mobiles (Platform Move 520)
//
//  Ce fichier heberge aussi le bus d'evenements VRLevelEvents : il permet aux
//  fonctionnalites VR-FP1-2 a VR-FP1-5 de demander un retour sonore / haptique
//  sans dependre directement du script VRFeedbackManager (VR-FP1-6).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// Types de retour (feedback) utilises par l'editeur de niveaux.
    /// </summary>
    public enum VRFeedbackType
    {
        Select, // un element vient d'etre selectionne / attrape
        Place,  // un element vient d'etre place / relache
        Delete, // un element vient d'etre supprime
        Save,   // le niveau vient d'etre exporte
        Error   // action refusee (element verrouille, rien de selectionne, ...)
    }

    /// <summary>
    /// Bus d'evenements statique de l'editeur de niveaux.
    /// Permet de decoupler les fonctionnalites les unes des autres :
    ///  - VRUIManager signale qu'un element vient d'etre cree (ElementSpawned)
    ///  - n'importe quelle fonctionnalite peut demander un feedback (FeedbackRequested)
    ///  - VRFeedbackManager (VR-FP1-6) s'abonne et joue le son / la vibration
    /// </summary>
    public static class VRLevelEvents
    {
        /// <summary>Evenement : un nouvel element vient d'etre cree dans le niveau.</summary>
        public static event Action<GameObject> ElementSpawned;

        /// <summary>Evenement : une fonctionnalite demande un retour sonore / haptique.</summary>
        public static event Action<VRFeedbackType> FeedbackRequested;

        public static void RaiseElementSpawned(GameObject element)
        {
            ElementSpawned?.Invoke(element);
        }

        public static void RaiseFeedback(VRFeedbackType type)
        {
            FeedbackRequested?.Invoke(type);
        }
    }

    /// <summary>
    /// Element editable du niveau (plateforme, piege, ...).
    /// </summary>
    [DisallowMultipleComponent]
    public class VR_EditableElement : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Registre des elements presents dans la scene (utilise par l'export et
        // par les autres gestionnaires : pas besoin de FindObjectsOfType).
        // ---------------------------------------------------------------------
        private static readonly List<VR_EditableElement> s_Registered = new List<VR_EditableElement>();

        /// <summary>Tous les elements editables actuellement presents dans la scene.</summary>
        public static IReadOnlyList<VR_EditableElement> All => s_Registered;

        // ---------------------------------------------------------------------
        // Donnees serialisees (visibles dans l'Inspector)
        // ---------------------------------------------------------------------

        [Header("Identification (utilise par l'export JSON)")]
        [Tooltip("Type logique de l'element ecrit dans le champ \"type\" du JSON.")]
        public string elementType = "Platform_Classical";

        [Tooltip("Un element verrouille ne peut pas etre supprime (VR-FP1-4).")]
        public bool isLocked = false;

        [Header("Mouvement optionnel (champ \"moveParams\" du JSON)")]
        [Tooltip("Cocher pour une plateforme mobile (ex : Platform Move 520).")]
        public bool hasMovement = false;

        [Tooltip("Decalage de depart par rapport a la position de pose (0 = la position ou l'utilisateur a pose l'element).")]
        public Vector3 moveFrom = Vector3.zero;

        [Tooltip("Decalage d'arrivee par rapport a la position de pose (ex : (0, 0, 4) = 4 m vers l'avant).")]
        public Vector3 moveTo = Vector3.zero;

        [Tooltip("Duree d'un aller (secondes). ")]
        public float duration = 3f;

        [Header("Apparence")]
        [Tooltip("Teinte appliquee a l'element lorsqu'il est selectionne.")]
        public Color selectionColor = new Color(1f, 0.62f, 0.13f, 1f);

        // ---------------------------------------------------------------------
        // Etat interne (non serialise)
        // ---------------------------------------------------------------------

        /// <summary>Vrai lorsque l'element est tenu par une main virtuelle (VR-FP1-3).</summary>
        [NonSerialized] public bool isHeld;

        /// <summary>Vrai lorsque l'element est selectionne (surbrillance active).</summary>
        [NonSerialized] public bool isSelected;

        private Renderer[] m_Renderers;
        private MaterialPropertyBlock m_PropertyBlock;
        private Color[] m_OriginalColors;
        private bool m_HasCachedColors;

        // Position de pose de l'element : les champs moveFrom / moveTo sont des
        // DECALAGES par rapport a cette position, ce qui permet de deplacer
        // l'element n'importe ou dans le niveau sans casser son mouvement.
        private Vector3 m_BasePosition;
        private bool m_BasePositionInitialized;

        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_ColorId = Shader.PropertyToID("_Color");

        // ---------------------------------------------------------------------
        // Cycle de vie Unity
        // ---------------------------------------------------------------------

        private void OnEnable()
        {
            if (!s_Registered.Contains(this))
            {
                s_Registered.Add(this);
            }
        }

        private void OnDisable()
        {
            s_Registered.Remove(this);
        }

        private void Awake()
        {
            CaptureBasePosition();
        }

        private void Start()
        {
            // Si un mouvement est active mais non renseigne, on fabrique un
            // trajet simple de 4 m vers l'avant : l'utilisateur pourra le
            // corriger dans l'Inspector (champs moveFrom / moveTo).
            if (hasMovement && moveFrom == moveTo)
            {
                moveFrom = Vector3.zero;
                moveTo = Vector3.forward * 4f;
            }
        }

        private void Update()
        {
            if (!hasMovement || duration <= 0f || isHeld) return;
            if (moveFrom == moveTo) return;

            // Aller-retour continu entre la position de depart et d'arrivee.
            float t = Mathf.PingPong(Time.time / duration, 1f);
            transform.position = GetBasePosition() + Vector3.Lerp(moveFrom, moveTo, t);
        }

        // ---------------------------------------------------------------------
        // API appelee par les autres scripts
        // ---------------------------------------------------------------------

        /// <summary>Nom du type d'element tel qu'il sera exporte.</summary>
        public string GetTypeName()
        {
            return string.IsNullOrEmpty(elementType) ? gameObject.name : elementType;
        }

        /// <summary>Position de pose (origine des decalages moveFrom / moveTo).</summary>
        public Vector3 GetBasePosition()
        {
            if (!m_BasePositionInitialized) CaptureBasePosition();
            return m_BasePosition;
        }

        /// <summary>Definit la nouvelle position de pose (appelee au relachement).</summary>
        public void SetBasePosition(Vector3 position)
        {
            m_BasePosition = position;
            m_BasePositionInitialized = true;
        }

        private void CaptureBasePosition()
        {
            m_BasePosition = transform.position;
            m_BasePositionInitialized = true;
        }

        /// <summary>Position monde du debut de trajet (utilisee par l'export JSON).</summary>
        public Vector3 WorldMoveStart => GetBasePosition() + moveFrom;

        /// <summary>Position monde de la fin de trajet (utilisee par l'export JSON).</summary>
        public Vector3 WorldMoveEnd => GetBasePosition() + moveTo;

        /// <summary>Appelee par VRGrabManager quand l'element est attrape.</summary>
        public void OnGrabbed()
        {
            isHeld = true;
        }

        /// <summary>
        /// Appelee par VRGrabManager quand l'element est relache : le trajet de
        /// mouvement repart de l'endroit ou l'utilisateur l'a pose.
        /// </summary>
        public void OnReleased()
        {
            isHeld = false;

            if (hasMovement)
            {
                SetBasePosition(transform.position);
            }
        }

        /// <summary>Active / desactive la surbrillance de selection.</summary>
        public void SetSelected(bool selected)
        {
            if (isSelected == selected) return;

            isSelected = selected;
            ApplyHighlight();
        }

        /// <summary>Verrouille ou deverrouille l'element (VR-FP1-4).</summary>
        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }

        // ---------------------------------------------------------------------
        // Surbrillance (sans instancier de material : MaterialPropertyBlock)
        // ---------------------------------------------------------------------

        private void CacheRenderers()
        {
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_PropertyBlock = new MaterialPropertyBlock();
            m_OriginalColors = new Color[m_Renderers.Length];

            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Material shared = m_Renderers[i].sharedMaterial;
                if (shared == null)
                {
                    m_OriginalColors[i] = Color.white;
                }
                else if (shared.HasProperty(s_BaseColorId)) // URP / HDRP
                {
                    m_OriginalColors[i] = shared.GetColor(s_BaseColorId);
                }
                else if (shared.HasProperty(s_ColorId)) // Built-in
                {
                    m_OriginalColors[i] = shared.GetColor(s_ColorId);
                }
                else
                {
                    m_OriginalColors[i] = Color.white;
                }
            }

            m_HasCachedColors = true;
        }

        private void ApplyHighlight()
        {
            if (!m_HasCachedColors || m_Renderers == null)
            {
                CacheRenderers();
            }

            if (m_Renderers == null) return;

            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Renderer renderer = m_Renderers[i];
                if (renderer == null) continue;

                Color color = isSelected
                    ? Color.Lerp(m_OriginalColors[i], selectionColor, 0.75f)
                    : m_OriginalColors[i];

                renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(s_BaseColorId, color);
                m_PropertyBlock.SetColor(s_ColorId, color);
                renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}
