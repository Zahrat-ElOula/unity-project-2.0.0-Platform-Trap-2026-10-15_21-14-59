using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FactoryFrenzy.VR
{
    [DisallowMultipleComponent]
    public class VRGrabManager : MonoBehaviour
    {
        [Header("Options de manipulation")]
        public bool autoAddGrabComponents = true;
        public bool snapToGridOnRelease = false;
        public float gridSize = 0.25f;
        public bool keepElementsFloating = true;
        public bool logGrabs = false;

        public event Action<bool> OnGrabStateChanged;
        public bool IsGrabbing { get; private set; }
        public VR_EditableElement CurrentElement { get; private set; }

        private readonly Dictionary<XRGrabInteractable, VR_EditableElement> m_Tracked =
            new Dictionary<XRGrabInteractable, VR_EditableElement>();

        private void OnEnable()
        {
            VRLevelEvents.ElementSpawned += RegisterElement;
            foreach (VR_EditableElement element in VR_EditableElement.All)
            {
                if (element != null) RegisterElement(element.gameObject);
            }
        }

        private void OnDisable()
        {
            VRLevelEvents.ElementSpawned -= RegisterElement;

            foreach (KeyValuePair<XRGrabInteractable, VR_EditableElement> pair in m_Tracked)
            {
                if (pair.Key == null) continue;
                pair.Key.selectEntered.RemoveListener(OnSelectEntered);
                pair.Key.selectExited.RemoveListener(OnSelectExited);
            }

            m_Tracked.Clear();
            CurrentElement = null;
            IsGrabbing = false;
        }

        private void LateUpdate()
        {
            // Sécurité : Si on tient un objet qui est verrouillé, on le relâche
            if (CurrentElement != null && CurrentElement.isLocked)
            {
                ForceReleaseCurrentElement();
            }
        }

        public void RegisterElement(GameObject elementObject)
        {
            if (elementObject == null) return;

            VR_EditableElement element = elementObject.GetComponent<VR_EditableElement>();
            if (element == null) element = elementObject.GetComponentInChildren<VR_EditableElement>();
            if (element == null) return;

            XRGrabInteractable grab = element.GetComponent<XRGrabInteractable>();

            if (grab == null && autoAddGrabComponents)
            {
                Rigidbody body = element.GetComponent<Rigidbody>();
                if (body == null) body = element.gameObject.AddComponent<Rigidbody>();

                body.isKinematic = true;
                body.useGravity = !keepElementsFloating;

                grab = element.gameObject.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;

                Debug.Log($"[VR-FP1-3] Composants d'attrape ajoutes automatiquement sur {element.name}.");
            }

            if (grab == null || m_Tracked.ContainsKey(grab)) return;

            m_Tracked.Add(grab, element);
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.selectExited.AddListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            VR_EditableElement element = GetElementFromArgs(args);
            if (element == null) return;

            if (element.isLocked)
            {
                Debug.Log($"[VR-FP2-2] Attrape refusee : {element.name} est verrouille.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                // Affiche le message dans le casque
                FindObjectOfType<VRUIManager>()?.ShowToast("Element verrouille !", 2f);
                return;
            }

            CurrentElement = element;
            IsGrabbing = true;

            element.OnGrabbed();
            element.SetSelected(true);

            VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
            OnGrabStateChanged?.Invoke(true);

            if (logGrabs) Debug.Log($"[VR-FP1-3] Element attrape : {element.GetTypeName()}.");
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            VR_EditableElement element = GetElementFromArgs(args);
            if (element == null) return;

            element.OnReleased();
            element.SetSelected(false);

            if (snapToGridOnRelease)
            {
                element.transform.position = SnapToGrid(element.transform.position);
            }

            CurrentElement = null;
            IsGrabbing = false;

            VRLevelEvents.RaiseFeedback(VRFeedbackType.Place);
            OnGrabStateChanged?.Invoke(false);

            if (logGrabs) Debug.Log($"[VR-FP1-3] Element place : {element.GetTypeName()} en {element.transform.position}.");
        }

        private VR_EditableElement GetElementFromArgs(SelectEnterEventArgs args)
        {
            if (args.interactableObject == null) return null;
            return args.interactableObject.transform.GetComponentInParent<VR_EditableElement>();
        }

        private VR_EditableElement GetElementFromArgs(SelectExitEventArgs args)
        {
            if (args.interactableObject == null) return null;
            return args.interactableObject.transform.GetComponentInParent<VR_EditableElement>();
        }

        public Vector3 SnapToGrid(Vector3 position)
        {
            if (gridSize <= 0f) return position;
            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                position.y,
                Mathf.Round(position.z / gridSize) * gridSize);
        }

        public void UnregisterElement(GameObject elementObject)
        {
            if (elementObject == null) return;
            XRGrabInteractable grab = elementObject.GetComponent<XRGrabInteractable>();
            if (grab == null || !m_Tracked.ContainsKey(grab)) return;

            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
            m_Tracked.Remove(grab);
        }

        public void ForceReleaseCurrentElement()
        {
            if (CurrentElement != null) CurrentElement.SetSelected(false);
            CurrentElement = null;
            IsGrabbing = false;
            OnGrabStateChanged?.Invoke(false);
        }
    }
}