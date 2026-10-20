using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace FactoryFrenzy.VR
{
    [DisallowMultipleComponent]
    public class VRDeleteManager : MonoBehaviour
    {
        [Header("References (auto-detectees si vides)")]
        public XRRayInteractor rayInteractor;
        public VRGrabManager grabManager;

        [Header("Entree - Suppression (Trigger Droit)")]
        [Tooltip("Action pour supprimer (Trigger de la main droite).")]
        public InputActionProperty deleteAction;

        [Header("Entree - Selection Multiple (Trigger Gauche)")]
        [Tooltip("Action pour la selection multiple (Trigger de la main gauche).")]
        public InputActionProperty selectionAction;

        [Header("Entree - Verrouillage (Y main gauche)")]
        [Tooltip("Action pour verrouiller/deverrouiller (Bouton Y de la main gauche).")]
        public InputActionProperty lockAction;

        [Header("Entree - Configuration Plateforme (VR-FP2-3)")]
        [Tooltip("Action pour definir le point de depart (Bouton A main droite).")]
        public InputActionProperty setStartAction;

        [Tooltip("Action pour definir le point d'arrivee (Bouton B main droite).")]
        public InputActionProperty setEndAction;

        [Header("Options")]
        public bool preventDeleteWhileGrabbing = true;
        public float maxDeleteDistance = 10f;
        public bool logDeletes = true;
        public bool enableMultiSelection = true;

        public System.Collections.Generic.IReadOnlyList<VR_EditableElement> SelectedElements => m_SelectedElements;
        public int DeleteCount { get; private set; }

        private readonly System.Collections.Generic.List<VR_EditableElement> m_SelectedElements =
            new System.Collections.Generic.List<VR_EditableElement>();

        private void Awake()
        {
            if (grabManager == null) grabManager = FindObjectOfType<VRGrabManager>();
            if (rayInteractor == null) rayInteractor = FindRightHandInteractionRay();
        }

        private void OnEnable()
        {
            if (deleteAction.action != null) { deleteAction.action.Enable(); deleteAction.action.performed += OnDeletePerformed; }
            if (selectionAction.action != null) { selectionAction.action.Enable(); selectionAction.action.performed += OnSelectionPerformed; }
            if (lockAction.action != null) { lockAction.action.Enable(); lockAction.action.performed += OnLockPerformed; }
            if (setStartAction.action != null) { setStartAction.action.Enable(); setStartAction.action.performed += OnSetStartPerformed; }
            if (setEndAction.action != null) { setEndAction.action.Enable(); setEndAction.action.performed += OnSetEndPerformed; }
        }

        private void OnDisable()
        {
            if (deleteAction.action != null) deleteAction.action.performed -= OnDeletePerformed;
            if (selectionAction.action != null) selectionAction.action.performed -= OnSelectionPerformed;
            if (lockAction.action != null) lockAction.action.performed -= OnLockPerformed;
            if (setStartAction.action != null) setStartAction.action.performed -= OnSetStartPerformed;
            if (setEndAction.action != null) setEndAction.action.performed -= OnSetEndPerformed;

            ClearSelection();
        }

        private void OnDeletePerformed(InputAction.CallbackContext context) => TryDelete();
        private void OnSelectionPerformed(InputAction.CallbackContext context) => ToggleTargetedSelection();
        private void OnLockPerformed(InputAction.CallbackContext context) => ToggleTargetedLock();
        private void OnSetStartPerformed(InputAction.CallbackContext context) => SetTargetedStartPosition();
        private void OnSetEndPerformed(InputAction.CallbackContext context) => SetTargetedEndPosition();

        public void ToggleTargetedLock()
        {
            VR_EditableElement element = GetTargetedElement();
            if (element != null)
            {
                element.ToggleLock();
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast(element.isLocked ? "Element verrouille" : "Element deverrouille", 2f);
            }
            else
            {
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Aucun element pointe !", 2f);
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
            }
        }

        public void SetTargetedStartPosition()
        {
            VR_EditableElement element = GetTargetedElement();
            if (element != null && element.GetTypeName() == "Platform_Move")
            {
                element.startPosition = GetRaycastPosition();
                Debug.Log($"[VR-FP2-3] Point de depart defini pour {element.name} en {element.startPosition}");
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Point de depart defini !", 2f);
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Place);
            }
            else if (element != null)
            {
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Cet element n'est pas une plateforme mobile !", 2f);
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
            }
        }

        public void SetTargetedEndPosition()
        {
            VR_EditableElement element = GetTargetedElement();
            if (element != null && element.GetTypeName() == "Platform_Move")
            {
                element.endPosition = GetRaycastPosition();
                Debug.Log($"[VR-FP2-3] Point d'arrivee defini pour {element.name} en {element.endPosition}");
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Point d'arrivee defini !", 2f);
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Place);
            }
            else if (element != null)
            {
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Cet element n'est pas une plateforme mobile !", 2f);
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
            }
        }

        private Vector3 GetRaycastPosition()
        {
            if (rayInteractor != null && rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
                return hit.point;
            return Vector3.zero;
        }

       // public bool TryDelete()
        //{
        //    if (grabManager != null && grabManager.CurrentElement != null)
        //    {
        //        VR_EditableElement heldElement = grabManager.CurrentElement;
        //        grabManager.ForceReleaseCurrentElement();
        //        Debug.Log("[VR-FP1-4] Suppression de l'element tenu en main depuis la palette.");
        //        return DeleteElement(heldElement);
        //    }

        //    if (preventDeleteWhileGrabbing && grabManager != null && grabManager.IsGrabbing)
        //    {
        //        Debug.Log("[VR-FP1-4] Suppression refusee : un element est en cours de manipulation.");
        //        VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
        //        return false;
        //    }

        //    if (m_SelectedElements.Count > 0) return DeleteSelected();

        //    VR_EditableElement element = GetTargetedElement();
        //    if (element == null)
        //    {
        //        VRUIManager ui = FindObjectOfType<VRUIManager>();
        //        if (ui != null) ui.ShowToast("Aucun element pointe !", 2f);
        //        VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
        //        return false;
        //    }
        //    return DeleteElement(element);
        //}

        public bool TryDelete()
        {
            // 1. Vérification : Ne rien faire si on clique sur l'UI
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            // 2. Vérification : Supprimer l'élément tenu en main
            if (grabManager != null && grabManager.CurrentElement != null)
            {
                VR_EditableElement heldElement = grabManager.CurrentElement;
                grabManager.ForceReleaseCurrentElement();
                Debug.Log("[VR-FP1-4] Suppression de l'element tenu en main depuis la palette.");
                return DeleteElement(heldElement);
            }

            // 3. Vérification : Interdire la suppression pendant une manipulation
            if (preventDeleteWhileGrabbing && grabManager != null && grabManager.IsGrabbing)
            {
                Debug.Log("[VR-FP1-4] Suppression refusee : un element est en cours de manipulation.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            // 4. Suppression multiple si des éléments sont sélectionnés
            if (m_SelectedElements.Count > 0) return DeleteSelected();

            // 5. Suppression de l'élément pointé
            VR_EditableElement element = GetTargetedElement();
            if (element == null)
            {
                // Message d'erreur seulement si on n'est pas sur l'UI
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Aucun element pointe !", 2f);
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            return DeleteElement(element);
        }

        public void ToggleTargetedSelection()
        {
            if (!enableMultiSelection) return;
            VR_EditableElement element = GetTargetedElement();
            if (element == null || element.isLocked)
            {
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Impossible de selectionner cet element !", 2f);
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
            Debug.Log($"[VR-FP1-4] Selection : {m_SelectedElements.Count} element(s). Trigger D pour supprimer.");
            VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
        }

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
            if (deleted > 0)
            {
                Debug.Log($"[VR-FP1-4] Suppression groupee : {deleted} element(s).");
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast($"{deleted} element(s) supprime(s)", 2f);
            }
            return deleted > 0;
        }

        public void ClearSelection()
        {
            foreach (VR_EditableElement element in m_SelectedElements)
                if (element != null) element.SetSelected(false);
            m_SelectedElements.Clear();
        }

        public void DeleteTargeted() => TryDelete();

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

        public bool DeleteElement(VR_EditableElement element)
        {
            if (element == null) return false;

            if (element.isLocked)
            {
                Debug.Log($"[VR-FP1-4] '{element.GetTypeName()}' est verrouille : suppression refusee.");
                VRUIManager ui = FindObjectOfType<VRUIManager>();
                if (ui != null) ui.ShowToast("Element verrouille : suppression refusee !", 2f);
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            VRLevelEvents.RaiseFeedback(VRFeedbackType.Delete);
            DeleteCount++;

            if (logDeletes)
                Debug.Log($"[VR-FP1-4] Element supprime : {element.GetTypeName()} (total : {DeleteCount}).");

            if (grabManager != null) grabManager.UnregisterElement(element.gameObject);
            m_SelectedElements.Remove(element);
            VRLevelEvents.RaiseElementDeleted(element.gameObject);

            Destroy(element.gameObject);
            return true;
        }

        public void DeleteAll()
        {
            foreach (VR_EditableElement element in VR_EditableElement.All)
            {
                if (element == null) continue;
                DeleteElement(element);
            }
        }

        private static XRRayInteractor FindRightHandInteractionRay()
        {
            XRRayInteractor fallback = null;
            foreach (XRRayInteractor ray in FindObjectsOfType<XRRayInteractor>())
            {
                string path = GetHierarchyPath(ray.transform).ToLowerInvariant();
                if (path.Contains("teleport")) continue;
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