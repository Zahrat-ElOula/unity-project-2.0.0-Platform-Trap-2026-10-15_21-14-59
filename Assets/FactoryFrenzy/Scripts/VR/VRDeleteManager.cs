// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VRDeleteManager.cs                 ->  fonctionnalite VR-FP1-4
//  ---------------------------------------------------------------------------
//  "Supprimer des elements du niveau"
//
//  Suppression par rayon : l'utilisateur pointe un element avec le rayon
//  d'interaction de la manette droite puis appuie sur le bouton B
//  (ou sur la touche Suppr / Retour arriere du clavier en test dans l'editeur).
//
//  Regles de securite :
//      * impossible de supprimer un element verrouille (isLocked) : retour
//        "erreur" (son grave + vibration forte) ;
//      * impossible de supprimer pendant une manipulation (element tenu) si
//        preventDeleteWhileGrabbing est actif ;
//      * portee limitee (maxDeleteDistance) pour eviter les suppressions
//        accidentelles de l'autre bout du niveau.
//
//  Le bouton "Supprimer" de la palette RV appelle simplement DeleteTargeted().
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// VR-FP1-4 : suppression d'elements du niveau.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRDeleteManager : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // References
        // ---------------------------------------------------------------------

        [Header("References (auto-detectees si vides)")]
        [Tooltip("Rayon d'interaction de la manette droite (XRRayInteractor).")]
        public XRRayInteractor rayInteractor;

        [Tooltip("Gestionnaire d'attrape : sert a interdire la suppression pendant une prise.")]
        public VRGrabManager grabManager;

        // ---------------------------------------------------------------------
        // Entree
        // ---------------------------------------------------------------------

        [Header("Entree")]
        [Tooltip("Creer automatiquement l'action par defaut (bouton B, Suppr, Retour arriere).")]
        public bool createDefaultInputAction = true;

        [Tooltip("Action personnalisee (optionnel) : remplace l'action par defaut.")]
        public InputActionProperty deleteAction;

        // ---------------------------------------------------------------------
        // Options
        // ---------------------------------------------------------------------

        [Header("Options")]
        public bool preventDeleteWhileGrabbing = true;
        public float maxDeleteDistance = 10f;
        public bool logDeletes = true;

        /// <summary>Nombre d'elements supprimes depuis le lancement.</summary>
        public int DeleteCount { get; private set; }

        private InputAction m_BuiltInAction;

        // ---------------------------------------------------------------------
        // Cycle de vie Unity
        // ---------------------------------------------------------------------

        private void Awake()
        {
            if (grabManager == null)
            {
                grabManager = FindObjectOfType<VRGrabManager>();
            }

            if (rayInteractor == null)
            {
                rayInteractor = FindRightHandInteractionRay();
            }
        }

        private void OnEnable()
        {
            SetupInput();
        }

        private void OnDisable()
        {
            if (m_BuiltInAction != null)
            {
                m_BuiltInAction.performed -= OnDeletePerformed;
                m_BuiltInAction.Disable();
                m_BuiltInAction.Dispose();
                m_BuiltInAction = null;
            }

            if (deleteAction.action != null)
            {
                deleteAction.action.performed -= OnDeletePerformed;
            }
        }

        // ---------------------------------------------------------------------
        // Entree (Input System)
        // ---------------------------------------------------------------------

        private void SetupInput()
        {
            // 1. Action fournie dans l'Inspector : elle est prioritaire.
            if (deleteAction.action != null)
            {
                deleteAction.action.Enable();
                deleteAction.action.performed += OnDeletePerformed;
                return;
            }

            if (!createDefaultInputAction) return;

            // 2. Action par defaut creee par code : aucun reglage a faire dans
            //    l'Inspector, et le clavier permet de tester sans casque.
            m_BuiltInAction = new InputAction("VR_Delete", InputActionType.Button);
            m_BuiltInAction.AddBinding("<XRController>{RightHand}/secondaryButton"); // bouton B
            m_BuiltInAction.AddBinding("<Keyboard>/delete");
            m_BuiltInAction.AddBinding("<Keyboard>/backspace");
            m_BuiltInAction.performed += OnDeletePerformed;
            m_BuiltInAction.Enable();
        }

        private void OnDeletePerformed(InputAction.CallbackContext context)
        {
            TryDelete();
        }

        // ---------------------------------------------------------------------
        // API publique
        // ---------------------------------------------------------------------

        /// <summary>
        /// Supprime l'element vise par le rayon. Renvoie true si un element a
        /// ete supprime.
        /// </summary>
        public bool TryDelete()
        {
            if (preventDeleteWhileGrabbing && grabManager != null && grabManager.IsGrabbing)
            {
                Debug.Log("[VR-FP1-4] Suppression refusee : un element est en cours de manipulation.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            VR_EditableElement element = GetTargetedElement();

            if (element == null)
            {
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            return DeleteElement(element);
        }

        /// <summary>Version void pour les UnityEvent (bouton "Supprimer" de la palette).</summary>
        public void DeleteTargeted()
        {
            TryDelete();
        }

        /// <summary>Element actuellement vise par le rayon d'interaction (ou null).</summary>
        public VR_EditableElement GetTargetedElement()
        {
            if (rayInteractor == null)
            {
                rayInteractor = FindRightHandInteractionRay();
                if (rayInteractor == null) return null;
            }

            if (!rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit)) return null;
            if (hit.distance > maxDeleteDistance) return null;

            return hit.transform.GetComponentInParent<VR_EditableElement>();
        }

        /// <summary>Supprime un element donne (respecte l'etat verrouille).</summary>
        public bool DeleteElement(VR_EditableElement element)
        {
            if (element == null) return false;

            if (element.isLocked)
            {
                Debug.Log($"[VR-FP1-4] '{element.GetTypeName()}' est verrouille : suppression refusee.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            // Retour sonore + vibration AVANT destruction de l'objet.
            VRLevelEvents.RaiseFeedback(VRFeedbackType.Delete);

            DeleteCount++;

            if (logDeletes)
            {
                Debug.Log($"[VR-FP1-4] Element supprime : {element.GetTypeName()} (total : {DeleteCount}).");
            }

            // Nettoyage des references avant destruction.
            if (grabManager != null) grabManager.UnregisterElement(element.gameObject);

            Destroy(element.gameObject);
            return true;
        }

        /// <summary>Supprime tous les elements du niveau (LevelRoot).</summary>
        public void DeleteAll()
        {
            foreach (VR_EditableElement element in VR_EditableElement.All)
            {
                if (element == null) continue;
                DeleteElement(element);
            }
        }

        // ---------------------------------------------------------------------
        // Recherche du rayon d'interaction
        // ---------------------------------------------------------------------

        /// <summary>
        /// Cherche le rayon d'interaction de la manette droite : on ignore les
        /// rayons de teleportation (geres par XR Interaction Toolkit).
        /// </summary>
        private static XRRayInteractor FindRightHandInteractionRay()
        {
            XRRayInteractor fallback = null;

            foreach (XRRayInteractor ray in FindObjectsOfType<XRRayInteractor>())
            {
                string path = GetHierarchyPath(ray.transform).ToLowerInvariant();

                if (path.Contains("teleport")) continue; // rayon de teleportation

                if (path.Contains("right")) return ray;

                if (fallback == null) fallback = ray;
            }

            return fallback;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;

            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
