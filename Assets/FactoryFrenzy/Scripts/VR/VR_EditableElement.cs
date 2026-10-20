using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FactoryFrenzy.VR
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class VR_EditableElement : MonoBehaviour
    {
        [Header("Type d'élément (pour le JSON)")]
        public string elementType;

        [Header("Paramètres pour la plateforme mobile")]
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float duration = 3f;

        [Header("État")]
        public bool isLocked = false;
        public bool hasMovement = false;

        [Header("Visuel Verrouillage (VR-FP2-2)")]
        public Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public Color lockedEmission = new Color(0.1f, 0.1f, 0.1f, 1f);

        public Vector3 moveFrom { get => startPosition; set => startPosition = value; }
        public Vector3 moveTo { get => endPosition; set => endPosition = value; }
        public Vector3 WorldMoveStart => startPosition;
        public Vector3 WorldMoveEnd => endPosition;

        private static readonly List<VR_EditableElement> s_All = new List<VR_EditableElement>();
        [System.NonSerialized] private MaterialPropertyBlock m_PropertyBlock;
        private bool m_IsSelected;
        private GameObject m_LockIcon;

        public static IReadOnlyList<VR_EditableElement> All => s_All;

        private void OnEnable()
        {
            if (!s_All.Contains(this)) s_All.Add(this);
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
                if (startPosition == Vector3.zero) startPosition = transform.position;
                if (endPosition == Vector3.zero) endPosition = startPosition + transform.forward * 4f;
            }
        }

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

        public string GetTypeName()
        {
            return string.IsNullOrEmpty(elementType) ? name : elementType;
        }

        public void OnGrabbed() { }
        public void OnReleased() { }

        public void ToggleLock() => SetLocked(!isLocked);

        public void SetLocked(bool locked)
        {
            if (isLocked == locked) return;
            isLocked = locked;
            UpdateVisualState();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Toujours kinematic en mode édition
                rb.constraints = locked ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.None;
            }

            XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = !locked; // Désactive l'attrape si verrouillé

            if (isLocked)
            {
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                Debug.Log($"[VR-FP2-2] Element verrouille : {name}");
                CreateLockIcon();
            }
            else
            {
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
                Debug.Log($"[VR-FP2-2] Element deverrouille : {name}");
                if (m_LockIcon != null) m_LockIcon.SetActive(false);
            }
        }

        private void CreateLockIcon()
        {
            if (m_LockIcon == null)
            {
                m_LockIcon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m_LockIcon.name = "LockIcon";
                m_LockIcon.transform.SetParent(transform);
                m_LockIcon.transform.localPosition = Vector3.up * 1.5f;
                m_LockIcon.transform.localScale = Vector3.one * 0.2f;
                Destroy(m_LockIcon.GetComponent<Collider>());

                Renderer r = m_LockIcon.GetComponent<Renderer>();
                if (r != null) r.material.color = Color.red;
            }
            m_LockIcon.SetActive(true);
        }

        public void SetSelected(bool selected)
        {
            if (m_IsSelected == selected) return;
            m_IsSelected = selected;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (m_PropertyBlock == null) m_PropertyBlock = new MaterialPropertyBlock();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.gameObject == m_LockIcon) continue;
                renderer.GetPropertyBlock(m_PropertyBlock);

                if (m_IsSelected)
                {
                    Color color = new Color(1f, 0.66f, 0.08f, 1f);
                    ApplyColorToBlock(m_PropertyBlock, renderer, color, color * 0.35f);
                }
                else if (isLocked)
                {
                    ApplyColorToBlock(m_PropertyBlock, renderer, lockedColor, lockedEmission);
                }
                else
                {
                    m_PropertyBlock.Clear();
                }
                renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        private void ApplyColorToBlock(MaterialPropertyBlock block, Renderer renderer, Color baseColor, Color emissionColor)
        {
            if (renderer.sharedMaterial == null) return;
            if (renderer.sharedMaterial.HasProperty("_BaseColor")) block.SetColor("_BaseColor", baseColor);
            if (renderer.sharedMaterial.HasProperty("_Color")) block.SetColor("_Color", baseColor);
            if (renderer.sharedMaterial.HasProperty("_EmissionColor")) block.SetColor("_EmissionColor", emissionColor);
        }

        public void AddDuration(float seconds)
        {
            duration = Mathf.Max(0.1f, duration + seconds);
            Debug.Log($"[VR-FP2-3] Nouvelle duree pour {name} : {duration:F1}s");
            VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
        }
    }
}