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

        [Header("Entree - Suppression")]
        public bool createDefaultInputAction = true;
        public InputActionProperty deleteAction;
        public InputActionProperty selectionAction;

        [Header("Entree - Verrouillage (VR-FP2-2)")]
        [Tooltip("Action pour verrouiller/deverrouiller l'element pointe (Bouton Y / Touche L).")]
        public InputActionProperty lockAction;

        [Header("Options")]
        public bool preventDeleteWhileGrabbing = true;
        public float maxDeleteDistance = 10f;
        public bool logDeletes = true;
        public bool enableMultiSelection = true;

        public System.Collections.Generic.IReadOnlyList<VR_EditableElement> SelectedElements => m_SelectedElements;
        public int DeleteCount { get; private set; }

        private InputAction m_BuiltInAction;
        private InputAction m_BuiltInSelectionAction;
        private InputAction m_BuiltInLockAction; // NOUVEAU
        private readonly System.Collections.Generic.List<VR_EditableElement> m_SelectedElements =
            new System.Collections.Generic.List<VR_EditableElement>();

        private void Awake()
        {
            if (grabManager == null) grabManager = FindObjectOfType<VRGrabManager>();
            if (rayInteractor == null) rayInteractor = FindRightHandInteractionRay();
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

            if (deleteAction.action != null) deleteAction.action.performed -= OnDeletePerformed;

            if (m_BuiltInSelectionAction != null)
            {
                m_BuiltInSelectionAction.performed -= OnSelectionPerformed;
                m_BuiltInSelectionAction.Disable();
                m_BuiltInSelectionAction.Dispose();
                m_BuiltInSelectionAction = null;
            }

            if (selectionAction.action != null) selectionAction.action.performed -= OnSelectionPerformed;

            // Nettoyage de l'action de verrouillage
            if (m_BuiltInLockAction != null)
            {
                m_BuiltInLockAction.performed -= OnLockPerformed;
                m_BuiltInLockAction.Disable();
                m_BuiltInLockAction.Dispose();
                m_BuiltInLockAction = null;
            }

            if (lockAction.action != null) lockAction.action.performed -= OnLockPerformed;

            ClearSelection();
        }

        private void SetupInput()
        {
            // --- SUPPRESSION ---
            if (deleteAction.action != null)
            {
                deleteAction.action.Enable();
                deleteAction.action.performed += OnDeletePerformed;
            }
            else if (createDefaultInputAction)
            {
                m_BuiltInAction = new InputAction("VR_Delete", InputActionType.Button);
                m_BuiltInAction.AddBinding("<XRController>{RightHand}/secondaryButton"); // B
                m_BuiltInAction.AddBinding("<Keyboard>/delete");
                m_BuiltInAction.AddBinding("<Keyboard>/backspace");
                m_BuiltInAction.performed += OnDeletePerformed;
                m_BuiltInAction.Enable();
            }

            // --- SELECTION MULTIPLE ---
            if (enableMultiSelection)
            {
                if (selectionAction.action != null)
                {
                    selectionAction.action.Enable();
                    selectionAction.action.performed += OnSelectionPerformed;
                }
                else
                {
                    m_BuiltInSelectionAction = new InputAction("VR_ToggleSelection", InputActionType.Button);
                    m_BuiltInSelectionAction.AddBinding("<XRController>{RightHand}/primaryButton"); // A
                    m_BuiltInSelectionAction.AddBinding("<Keyboard>/tab");
                    m_BuiltInSelectionAction.performed += OnSelectionPerformed;
                    m_BuiltInSelectionAction.Enable();
                }
            }

            // --- VERROUILLAGE (VR-FP2-2) ---
            if (lockAction.action != null)
            {
                lockAction.action.Enable();
                lockAction.action.performed += OnLockPerformed;
            }
            else
            {
                m_BuiltInLockAction = new InputAction("VR_Lock", InputActionType.Button);
                m_BuiltInLockAction.AddBinding("<XRController>{RightHand}/primaryButton"); // Bouton Y
                m_BuiltInLockAction.AddBinding("<Keyboard>/l");
                m_BuiltInLockAction.performed += OnLockPerformed;
                m_BuiltInLockAction.Enable();
            }
        }

        private void OnDeletePerformed(InputAction.CallbackContext context) => TryDelete();
        private void OnSelectionPerformed(InputAction.CallbackContext context) => ToggleTargetedSelection();
        private void OnLockPerformed(InputAction.CallbackContext context) => ToggleTargetedLock();

        public void ToggleTargetedLock()
        {
            VR_EditableElement element = GetTargetedElement();
            if (element != null)
            {
                element.ToggleLock();
            }
            else
            {
                Debug.Log("[VR-FP2-2] Aucun element pointe pour changer son verrouillage.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
            }
        }

        public bool TryDelete()
        {
            if (grabManager != null && grabManager.CurrentElement != null)
            {
                VR_EditableElement heldElement = grabManager.CurrentElement;
                grabManager.ForceReleaseCurrentElement();
                Debug.Log("[VR-FP1-4] Suppression de l'element tenu en main depuis la palette.");
                return DeleteElement(heldElement);
            }

            if (preventDeleteWhileGrabbing && grabManager != null && grabManager.IsGrabbing)
            {
                Debug.Log("[VR-FP1-4] Suppression refusee : un element est en cours de manipulation.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                return false;
            }

            if (m_SelectedElements.Count > 0) return DeleteSelected();

            VR_EditableElement element = GetTargetedElement();
            if (element == null)
            {
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