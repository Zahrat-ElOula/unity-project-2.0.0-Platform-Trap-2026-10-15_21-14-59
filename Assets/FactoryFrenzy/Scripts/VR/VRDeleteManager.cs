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

        [Tooltip("Action de selection multiple (A de la manette droite / Tab au clavier).")]
        public InputActionProperty selectionAction;

        // ---------------------------------------------------------------------
        // Options
        // ---------------------------------------------------------------------

        [Header("Options")]
        public bool preventDeleteWhileGrabbing = true;
        public float maxDeleteDistance = 10f;
        public bool logDeletes = true;

        [Tooltip("Autoriser la selection de plusieurs elements avec A/Tab, puis leur suppression avec B/Suppr.")]
        public bool enableMultiSelection = true;

        /// <summary>Elements marques en jaune pour une suppression groupee.</summary>
        public System.Collections.Generic.IReadOnlyList<VR_EditableElement> SelectedElements => m_SelectedElements;

        /// <summary>Nombre d'elements supprimes depuis le lancement.</summary>
        public int DeleteCount { get; private set; }

        private InputAction m_BuiltInAction;
        private InputAction m_BuiltInSelectionAction;
        private readonly System.Collections.Generic.List<VR_EditableElement> m_SelectedElements =
            new System.Collections.Generic.List<VR_EditableElement>();

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

            if (m_BuiltInSelectionAction != null)
            {
                m_BuiltInSelectionAction.performed -= OnSelectionPerformed;
                m_BuiltInSelectionAction.Disable();
                m_BuiltInSelectionAction.Dispose();
                m_BuiltInSelectionAction = null;
            }

            if (selectionAction.action != null)
                selectionAction.action.performed -= OnSelectionPerformed;

            ClearSelection();
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
                SetupSelectionInput();
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
            SetupSelectionInput();
        }

        private void OnDeletePerformed(InputAction.CallbackContext context)
        {
            TryDelete();
        }

        private void SetupSelectionInput()
        {
            if (!enableMultiSelection) return;
            if (selectionAction.action != null)
            {
                selectionAction.action.Enable();
                selectionAction.action.performed += OnSelectionPerformed;
                return;
            }

            m_BuiltInSelectionAction = new InputAction("VR_ToggleSelection", InputActionType.Button);
            m_BuiltInSelectionAction.AddBinding("<XRController>{RightHand}/primaryButton"); // bouton A
            m_BuiltInSelectionAction.AddBinding("<Keyboard>/tab");
            m_BuiltInSelectionAction.performed += OnSelectionPerformed;
            m_BuiltInSelectionAction.Enable();
        }

        private void OnSelectionPerformed(InputAction.CallbackContext context)
        {
            ToggleTargetedSelection();
        }

        // ---------------------------------------------------------------------
        // API publique
        // ---------------------------------------------------------------------

        /// <summary>
        /// Supprime l'element vise par le rayon. Renvoie true si un element a
        /// ete supprime.
        /// </summary>
        //public bool TryDelete()
        //{
        //    if (preventDeleteWhileGrabbing && grabManager != null && grabManager.IsGrabbing)
        //    {
        //        Debug.Log("[VR-FP1-4] Suppression refusee : un element est en cours de manipulation.");
        //        VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
        //        return false;
        //    }

        //    if (m_SelectedElements.Count > 0)
        //        return DeleteSelected();

        //    VR_EditableElement element = GetTargetedElement();
        //    if (element == null)
        //    {
        //        VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
        //        return false;
        //    }
        //    return DeleteElement(element);
        //}
        public bool TryDelete()
        {
            // 1. Si le joueur tient un objet, on le supprime directement
            if (grabManager != null && grabManager.CurrentElement != null)
            {
                VR_EditableElement heldElement = grabManager.CurrentElement;

                // On utilise la nouvelle méthode publique pour relâcher proprement
                grabManager.ForceReleaseCurrentElement();

                Debug.Log("[VR-FP1-4] Suppression de l'element tenu en main depuis la palette.");
                return DeleteElement(heldElement);
            }

            // 2. Sécurité : si on essaie de supprimer pendant une manipulation
            if (preventDeleteWhileGrabbing && grabManager != null && grabManager.IsGrabbing)
            {
                Debug.Log("[VR-FP1-4] Suppression refusee : un element est en cours de manipulation.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            // 3. Sinon, on utilise le rayon
            if (m_SelectedElements.Count > 0)
                return DeleteSelected();

            VR_EditableElement element = GetTargetedElement();
            if (element == null)
            {
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }
            return DeleteElement(element);
        }
        /// <summary>Ajoute/retire du groupe l'element vise par le rayon (bouton A/Tab).</summary>
        public void ToggleTargetedSelection()
        {
            if (!enableMultiSelection) return;
            VR_EditableElement element = GetTargetedElement();
            if (element == null || element.isLocked)
            {
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return;
            }

            if (m_SelectedElements.Contains(element))
            {
                m_SelectedElements.Remove(element);
                element.SetSelected(false);
            }
            else
            {
                m_SelectedElements.Add(element);
                element.SetSelected(true);
            }
            Debug.Log($"[VR-FP1-4] Selection : {m_SelectedElements.Count} element(s). B/Suppr pour supprimer.");
            VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
        }

        /// <summary>Supprime tous les elements actuellement selectionnes.</summary>
        public bool DeleteSelected()
        {
            int deleted = 0;
            VR_EditableElement[] selection = m_SelectedElements.ToArray();
            m_SelectedElements.Clear();
            foreach (VR_EditableElement element in selection)
            {
                if (element == null || element.isLocked) continue;
                element.SetSelected(false);
                if (DeleteElement(element)) deleted++;
            }
            if (deleted > 0) Debug.Log($"[VR-FP1-4] Suppression groupee : {deleted} element(s).");
            return deleted > 0;
        }

        /// <summary>Annule la selection multiple courante.</summary>
        public void ClearSelection()
        {
            foreach (VR_EditableElement element in m_SelectedElements)
                if (element != null) element.SetSelected(false);
            m_SelectedElements.Clear();
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
            m_SelectedElements.Remove(element);
            VRLevelEvents.RaiseElementDeleted(element.gameObject);

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
