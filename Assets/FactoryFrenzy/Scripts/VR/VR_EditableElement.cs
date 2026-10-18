using System.Collections.Generic;
using UnityEngine;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// Marqueur d'un élément éditable dans le niveau.
    /// Contient toutes les données nécessaires à l'export JSON.
    /// </summary>
    [DisallowMultipleComponent]
    public class VR_EditableElement : MonoBehaviour
    {
        [Header("Type d'élément (pour le JSON)")]
        [Tooltip("Ex: Platform_Start, Platform_Classical, Trap_Bumper, etc.")]
        public string elementType;

        [Header("Paramètres pour la plateforme mobile")]
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float duration = 3f;

        [Header("État")]
        public bool isLocked = false;
        public bool hasMovement = false;

        // -----------------------------------------------------------------
        // Alias pour compatibilité avec VRSceneBuilder (moveFrom / moveTo)
        // -----------------------------------------------------------------

        /// <summary>Alias de startPosition (utilisé par VRSceneBuilder).</summary>
        public Vector3 moveFrom
        {
            get => startPosition;
            set => startPosition = value;
        }

        /// <summary>Alias de endPosition (utilisé par VRSceneBuilder).</summary>
        public Vector3 moveTo
        {
            get => endPosition;
            set => endPosition = value;
        }

        // -----------------------------------------------------------------
        // Propriétés "monde" pour l'export JSON
        // -----------------------------------------------------------------

        public Vector3 WorldMoveStart => startPosition;
        public Vector3 WorldMoveEnd => endPosition;

        // -----------------------------------------------------------------
        // Cache global de tous les éléments éditables
        // -----------------------------------------------------------------

        private static readonly List<VR_EditableElement> s_All = new List<VR_EditableElement>();
        [System.NonSerialized] private MaterialPropertyBlock m_PropertyBlock;
        private bool m_IsSelected;

        public static IReadOnlyList<VR_EditableElement> All => s_All;

        // -----------------------------------------------------------------
        // Cycle de vie
        // -----------------------------------------------------------------

        private void OnEnable()
        {
            if (!s_All.Contains(this))
                s_All.Add(this);
        }

        private void OnDisable()
        {
            s_All.Remove(this);
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(elementType))
                ConfigureFromSourcePrefab(name);

            if (hasMovement)
            {
                if (startPosition == Vector3.zero)
                    startPosition = transform.position;

                if (endPosition == Vector3.zero)
                    endPosition = startPosition + transform.forward * 4f;
            }
        }

        /// <summary>
        /// Configure les donnees d'un element cree depuis les prefabs source du
        /// projet (Assets/FactoryFrenzy/Prefabs/Platform and Trap). Les assets
        /// d'origine restent ainsi utilisables par l'application PC du binome.
        /// </summary>
        /// <summary>Retourne le type stable exporte pour un prefab source.</summary>
        public static string GetTypeForSourcePrefab(string prefabName)
        {
            string key = (prefabName ?? string.Empty).Replace("(Clone)", string.Empty).Trim().ToLowerInvariant();
            switch (key)
            {
                case "platform start": return "Platform_Start";
                case "platform end": return "Platform_End";
                case "platform checkpoint": return "Platform_Checkpoint";
                case "platform classical 80":
                case "platform classical 360":
                case "platform classical 520": return "Platform_Classical";
                case "platform move 520": return "Platform_Move";
                case "platform trampoline": return "Trap_Trampoline";
                case "trap bumper": return "Trap_Bumper";
                case "trap fan": return "Trap_Fan";
                case "trap shooter": return "Trap_Shooter";
                default: return prefabName;
            }
        }

        public void ConfigureFromSourcePrefab(string prefabName)
        {
            elementType = GetTypeForSourcePrefab(prefabName);
            hasMovement = elementType == "Platform_Move";
            duration = 3f;

            if (hasMovement)
            {
                startPosition = transform.position;
                endPosition = startPosition + transform.forward * 4f;
            }
        }

        // -----------------------------------------------------------------
        // Méthodes utilisées par les autres scripts
        // -----------------------------------------------------------------

        public string GetTypeName()
        {
            return string.IsNullOrEmpty(elementType) ? name : elementType;
        }

        public void OnGrabbed() { }
        public void OnReleased() { }

        /// <summary>Met en evidence l'element cible/selectionne sans instancier de materiau.</summary>
        public void SetSelected(bool selected)
        {
            if (m_IsSelected == selected) return;
            m_IsSelected = selected;
            if (m_PropertyBlock == null) m_PropertyBlock = new MaterialPropertyBlock();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(m_PropertyBlock);
                if (selected)
                {
                    Color color = new Color(1f, 0.66f, 0.08f, 1f);
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                        m_PropertyBlock.SetColor("_BaseColor", color);
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                        m_PropertyBlock.SetColor("_Color", color);
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_EmissionColor"))
                        m_PropertyBlock.SetColor("_EmissionColor", color * 0.35f);
                }
                else
                {
                    // Les objets de l'editeur n'ont pas d'autre PropertyBlock runtime.
                    m_PropertyBlock.Clear();
                }
                renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}