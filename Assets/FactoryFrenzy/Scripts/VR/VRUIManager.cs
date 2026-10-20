using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace FactoryFrenzy.VR
{
    [DisallowMultipleComponent]
    public class VRUIManager : MonoBehaviour
    {
        [Header("Prefabs disponibles")]
        public List<GameObject> elementPrefabs = new List<GameObject>();
        public string[] elementLabels;

        [Header("References de scene")]
        public Transform levelRoot;
        public Transform spawnPoint;

        [Header("Creation")]
        public bool spawnInFrontOfHead = true;
        public float spawnDistance = 1.3f;
        public float spawnHeight = 1f;
        [Min(0.01f)] public float spawnSurfaceClearance = 0.08f;
        public string[] visualGroundKeywords = { "ground", "dirt", "terrain", "floor", "land" };

        [Header("Palette RV")]
        public bool followHead = true;
        public float paletteDistance = 1.7f;
        public float paletteVerticalOffset = -0.08f;
        public float paletteHorizontalOffset = -0.8f;
        public Vector2 paletteSize = new Vector2(560f, 760f);
        public float paletteScale = 0.0012f;

        [Header("Evenements de la palette")]
        public UnityEvent onDeleteRequested = new UnityEvent();
        public UnityEvent onExportRequested = new UnityEvent();

        public int SelectedIndex { get; private set; }
        public GameObject SelectedPrefab =>
            (elementPrefabs != null && SelectedIndex >= 0 && SelectedIndex < elementPrefabs.Count)
                ? elementPrefabs[SelectedIndex]
                : null;

        public ElementCategory ActiveCategory { get; private set; } = ElementCategory.Platforms;

        public enum ElementCategory { Platforms, Traps }

        private sealed class ElementButton
        {
            public int index;
            public ElementCategory category;
            public GameObject gameObject;
            public Image background;
        }

        private Canvas m_Canvas;
        private Transform m_CanvasTransform;
        private TMP_Text m_StatusText;
        private TMP_Text m_ToastText;
        private GameObject m_ToastBackground;
        private float m_ToastTimer = 0f;
        private TMP_InputField m_LevelNameInput;
        private readonly List<ElementButton> m_ElementButtons = new List<ElementButton>();
        private readonly Dictionary<ElementCategory, Image> m_CategoryButtonBackgrounds = new Dictionary<ElementCategory, Image>();
        private Camera m_Camera;
        private VRGrabManager m_GrabManager;

        private GameObject m_TabContentCreation;
        private GameObject m_TabContentEdition;
        private GameObject m_TabContentSysteme;
        private Button m_TabButtonCreation;
        private Button m_TabButtonEdition;
        private Button m_TabButtonSysteme;

        private static readonly Color s_ButtonColor = new Color(0.16f, 0.18f, 0.22f, 0.95f);
        private static readonly Color s_SelectedColor = new Color(0.95f, 0.55f, 0.10f, 1f);
        private static readonly Color s_ActionColor = new Color(0.20f, 0.35f, 0.55f, 0.95f);
        private static readonly Color s_TabActiveColor = new Color(0.20f, 0.50f, 0.80f, 1f);
        private static readonly Color s_TabInactiveColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        private void Start()
        {
            m_Camera = FindPlayerCamera();
            m_GrabManager = FindObjectOfType<VRGrabManager>();
            BuildPalette();
            if (elementPrefabs != null && elementPrefabs.Count > 0)
                SelectElement(FindFirstIndex(ActiveCategory));
        }

        private void LateUpdate()
        {
            if (!followHead || m_CanvasTransform == null) return;
            if (m_Camera == null) m_Camera = FindPlayerCamera();
            if (m_Camera == null) return;
            PlacePaletteInFrontOfCamera();
        }

        private void Update()
        {
            if (m_ToastTimer > 0f)
            {
                m_ToastTimer -= Time.deltaTime;
                if (m_ToastTimer <= 0f && m_ToastBackground != null)
                {
                    m_ToastBackground.SetActive(false);
                }
            }
        }

        public void ShowToast(string message, float duration = 2.5f)
        {
            if (m_ToastText == null || m_ToastBackground == null) return;
            m_ToastText.text = message;
            m_ToastBackground.SetActive(true);
            m_ToastTimer = duration;
        }

        private static Camera FindPlayerCamera()
        {
            Camera main = Camera.main;
            if (main != null) return main;
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera camera in cameras)
                if (camera.enabled && camera.gameObject.activeInHierarchy) return camera;
            return cameras.Length > 0 ? cameras[0] : null;
        }

        public void BuildPalette()
        {
            if (m_Canvas != null) return;

            GameObject canvasGo = new GameObject("VR_Palette");
            m_Canvas = canvasGo.AddComponent<Canvas>();
            m_CanvasTransform = canvasGo.transform;
            m_CanvasTransform.localScale = Vector3.one * paletteScale;
            m_Canvas.renderMode = RenderMode.WorldSpace;
            m_Canvas.worldCamera = m_Camera;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = paletteSize;

            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            Image background = canvasGo.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
            background.raycastTarget = false;

            VerticalLayoutGroup layout = canvasGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // 1. Status
            m_StatusText = CreateText(canvasGo.transform, "Selection : -", 20f, 34f);

            // 2. Toast (messages clairs)
            m_ToastBackground = new GameObject("ToastBackground", typeof(RectTransform));
            m_ToastBackground.transform.SetParent(canvasGo.transform, false);
            Image toastImg = m_ToastBackground.AddComponent<Image>();
            toastImg.color = new Color(0f, 0f, 0f, 0.9f);
            LayoutElement toastLayout = m_ToastBackground.AddComponent<LayoutElement>();
            toastLayout.preferredHeight = 60f;
            toastLayout.minHeight = 60f;

            m_ToastText = CreateText(m_ToastBackground.transform, "", 26f, 60f);
            m_ToastText.color = Color.yellow;
            m_ToastText.fontStyle = FontStyles.Bold;
            m_ToastBackground.SetActive(false);

            // 3. Barre d'onglets
            GameObject tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(canvasGo.transform, false);
            HorizontalLayoutGroup tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = false;
            LayoutElement tabBarLayout = tabBar.AddComponent<LayoutElement>();
            tabBarLayout.preferredHeight = 40f;

            m_TabButtonCreation = CreateButton(tabBar.transform, "CREER", s_TabInactiveColor, 40f);
            m_TabButtonCreation.onClick.AddListener(() => SetActiveTab(0));
            m_TabButtonEdition = CreateButton(tabBar.transform, "EDITER", s_TabInactiveColor, 40f);
            m_TabButtonEdition.onClick.AddListener(() => SetActiveTab(1));
            m_TabButtonSysteme = CreateButton(tabBar.transform, "SYSTEME", s_TabInactiveColor, 40f);
            m_TabButtonSysteme.onClick.AddListener(() => SetActiveTab(2));

            // 4. Contenu des onglets
            m_TabContentCreation = CreateTabContent(canvasGo.transform, "CreationContent");
            m_TabContentEdition = CreateTabContent(canvasGo.transform, "EditionContent");
            m_TabContentSysteme = CreateTabContent(canvasGo.transform, "SystemeContent");

            // --- Onglet CREATION ---
            GameObject categories = new GameObject("Categories", typeof(RectTransform));
            categories.transform.SetParent(m_TabContentCreation.transform, false);
            HorizontalLayoutGroup categoryLayout = categories.AddComponent<HorizontalLayoutGroup>();
            categoryLayout.spacing = 6f;
            categoryLayout.childForceExpandWidth = true;
            categoryLayout.childForceExpandHeight = false;
            LayoutElement categoryElement = categories.AddComponent<LayoutElement>();
            categoryElement.preferredHeight = 40f;
            AddCategoryButton(categories.transform, "PLATEFORMES", ElementCategory.Platforms, new Color(0.10f, 0.42f, 0.80f, 0.98f));
            AddCategoryButton(categories.transform, "PIEGES", ElementCategory.Traps, new Color(0.75f, 0.22f, 0.12f, 0.98f));

            for (int i = 0; i < elementPrefabs.Count; i++)
            {
                int index = i;
                GameObject prefab = elementPrefabs[i];
                if (prefab == null) continue;

                Button button = CreateButton(m_TabContentCreation.transform, GetPrefabLabel(prefab, i), s_ButtonColor, 38f);
                button.onClick.AddListener(() => SelectElement(index));

                Image buttonImage = button.targetGraphic as Image;
                m_ElementButtons.Add(new ElementButton
                {
                    index = index,
                    category = GetCategory(prefab),
                    gameObject = button.gameObject,
                    background = buttonImage
                });
            }

            Button createButton = CreateButton(m_TabContentCreation.transform, "CREER L'ELEMENT", s_ActionColor, 46f);
            createButton.onClick.AddListener(CreateSelected);

            // --- Onglet EDITER ---
            Button lockButton = CreateButton(m_TabContentEdition.transform, "VERROUILLER L'ELEMENT", new Color(0.5f, 0.5f, 0.5f, 1f), 42f);
            lockButton.onClick.AddListener(ToggleLockOnHeldOrTargeted);

            Button deleteButton = CreateButton(m_TabContentEdition.transform, "SUPPRIMER SELECTION", new Color(0.65f, 0.16f, 0.14f, 0.98f), 42f);
            deleteButton.onClick.AddListener(() => onDeleteRequested.Invoke());

            // --- Onglet SYSTEME ---
            Button timePlusButton = CreateButton(m_TabContentSysteme.transform, "Temps Plateforme +1s", new Color(0.2f, 0.6f, 0.2f, 0.98f), 38f);
            timePlusButton.onClick.AddListener(() => ModifyDuration(1f));

            Button timeMinusButton = CreateButton(m_TabContentSysteme.transform, "Temps Plateforme -1s", new Color(0.6f, 0.2f, 0.2f, 0.98f), 38f);
            timeMinusButton.onClick.AddListener(() => ModifyDuration(-1f));

            m_LevelNameInput = CreateInputField(m_TabContentSysteme.transform, "Nom du niveau", "DemoScene_Forest", 38f);
            m_LevelNameInput.onValueChanged.AddListener(SetExportFileName);
            SetExportFileName(m_LevelNameInput.text);

            Button exportButton = CreateButton(m_TabContentSysteme.transform, "EXPORTER JSON", new Color(0.30f, 0.28f, 0.65f, 0.98f), 42f);
            exportButton.onClick.AddListener(() => onExportRequested.Invoke());

            // Footer d'aide
            CreateText(canvasGo.transform, "A/1 : depart | B/2 : arrivee | Y/L : verrouiller", 13f, 26f);

            SetActiveTab(0);
            SetCategory(ActiveCategory, false);
            PlacePaletteInFrontOfCamera();
        }

        private GameObject CreateTabContent(Transform parent, string name)
        {
            GameObject content = new GameObject(name, typeof(RectTransform));
            content.transform.SetParent(parent, false);
            VerticalLayoutGroup vLayout = content.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 4f;
            vLayout.childForceExpandHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childControlWidth = true;
            LayoutElement layoutElement = content.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            return content;
        }

        private void SetActiveTab(int tabIndex)
        {
            m_TabContentCreation.SetActive(tabIndex == 0);
            m_TabContentEdition.SetActive(tabIndex == 1);
            m_TabContentSysteme.SetActive(tabIndex == 2);

            m_TabButtonCreation.image.color = tabIndex == 0 ? s_TabActiveColor : s_TabInactiveColor;
            m_TabButtonEdition.image.color = tabIndex == 1 ? s_TabActiveColor : s_TabInactiveColor;
            m_TabButtonSysteme.image.color = tabIndex == 2 ? s_TabActiveColor : s_TabInactiveColor;
        }

        public void ToggleLockOnHeldOrTargeted()
        {
            VR_EditableElement target = null;
            if (m_GrabManager != null && m_GrabManager.CurrentElement != null) target = m_GrabManager.CurrentElement;
            else
            {
                VRDeleteManager deleteManager = FindObjectOfType<VRDeleteManager>();
                if (deleteManager != null) target = deleteManager.GetTargetedElement();
            }

            if (target != null)
            {
                target.ToggleLock();
                ShowToast(target.isLocked ? "Element verrouille" : "Element deverrouille", 2f);
            }
            else
            {
                ShowToast("Pointez ou tenez un element !", 2f);
            }
        }

        private void ModifyDuration(float amount)
        {
            VR_EditableElement target = null;

            if (m_GrabManager != null && m_GrabManager.CurrentElement != null)
                target = m_GrabManager.CurrentElement;
            else
            {
                VRDeleteManager deleteManager = FindObjectOfType<VRDeleteManager>();
                if (deleteManager != null) target = deleteManager.GetTargetedElement();
            }

            if (target != null && target.GetTypeName() == "Platform_Move")
            {
                target.AddDuration(amount);
                ShowToast($"Duree : {target.duration:F1}s");
            }
            else
            {
                ShowToast("Pointez ou tenez une plateforme mobile !", 2f);
            }
        }

        private void AddCategoryButton(Transform parent, string label, ElementCategory category, Color color)
        {
            Button button = CreateButton(parent, label, color, 40f);
            button.onClick.AddListener(() => SetCategory(category));
            m_CategoryButtonBackgrounds[category] = button.targetGraphic as Image;
        }

        public void SetCategory(ElementCategory category) => SetCategory(category, true);

        private void SetCategory(ElementCategory category, bool feedback)
        {
            ActiveCategory = category;
            foreach (ElementButton entry in m_ElementButtons)
                entry.gameObject.SetActive(entry.category == category);

            foreach (KeyValuePair<ElementCategory, Image> entry in m_CategoryButtonBackgrounds)
            {
                if (entry.Value == null) continue;
                Color color = entry.Key == ElementCategory.Platforms
                    ? new Color(0.10f, 0.42f, 0.80f, 0.98f)
                    : new Color(0.75f, 0.22f, 0.12f, 0.98f);
                entry.Value.color = entry.Key == category ? Color.Lerp(color, Color.white, 0.15f) : color * 0.62f;
            }

            if (feedback) VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
            UpdatePaletteState();
        }

        private static ElementCategory GetCategory(GameObject prefab)
        {
            VR_EditableElement element = prefab != null ? prefab.GetComponent<VR_EditableElement>() : null;
            string type = element != null && !string.IsNullOrEmpty(element.elementType)
                ? element.elementType
                : VR_EditableElement.GetTypeForSourcePrefab(prefab != null ? prefab.name : string.Empty);
            return type.StartsWith("Trap_", System.StringComparison.OrdinalIgnoreCase)
                ? ElementCategory.Traps
                : ElementCategory.Platforms;
        }

        private int FindFirstIndex(ElementCategory category)
        {
            for (int i = 0; i < elementPrefabs.Count; i++)
                if (elementPrefabs[i] != null && GetCategory(elementPrefabs[i]) == category) return i;
            return 0;
        }

        public void SetExportFileName(string fileName)
        {
            VRLevelExporter exporter = FindObjectOfType<VRLevelExporter>();
            if (exporter != null) exporter.SetLevelName(fileName);
        }

        private static TMP_InputField CreateInputField(Transform parent, string placeholderText, string initialValue, float height)
        {
            GameObject go = new GameObject("Nom du fichier", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;

            TMP_Text text = CreateText(go.transform, initialValue, 18f, height);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(10f, 0f, 10f, 0f);
            TMP_Text placeholder = CreateText(go.transform, placeholderText, 18f, height);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.7f, 0.7f, 0.7f, 0.75f);
            placeholder.margin = new Vector4(10f, 0f, 10f, 0f);

            input.textViewport = go.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = initialValue;
            return input;
        }

        private string GetPrefabLabel(GameObject prefab, int index)
        {
            if (elementLabels != null && index < elementLabels.Length &&
                !string.IsNullOrEmpty(elementLabels[index]))
            {
                return elementLabels[index];
            }

            VR_EditableElement element = prefab.GetComponent<VR_EditableElement>();
            if (element != null && !string.IsNullOrEmpty(element.elementType))
            {
                return element.elementType;
            }

            return prefab.name;
        }

        private void PlacePaletteInFrontOfCamera()
        {
            if (m_CanvasTransform == null) return;

            Vector3 origin = m_Camera != null ? m_Camera.transform.position : Vector3.zero + Vector3.up * 1.6f;
            Vector3 forward = m_Camera != null ? m_Camera.transform.forward : Vector3.forward;

            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 right = m_Camera != null ? m_Camera.transform.right : Vector3.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
            m_CanvasTransform.position = origin + flatForward * paletteDistance +
                                         right * paletteHorizontalOffset + Vector3.up * paletteVerticalOffset;

            Vector3 lookDirection = origin - m_CanvasTransform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                m_CanvasTransform.rotation = Quaternion.LookRotation(-lookDirection.normalized, Vector3.up);
            }
        }

        public void SelectElement(int index)
        {
            if (elementPrefabs == null || elementPrefabs.Count == 0)
            {
                SelectedIndex = 0;
                UpdatePaletteState();
                return;
            }

            SelectedIndex = Mathf.Clamp(index, 0, elementPrefabs.Count - 1);
            if (SelectedPrefab != null && GetCategory(SelectedPrefab) != ActiveCategory)
                SetCategory(GetCategory(SelectedPrefab), false);
            VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
            UpdatePaletteState();
        }

        public void SelectElementByType(string elementType)
        {
            if (string.IsNullOrEmpty(elementType) || elementPrefabs == null) return;

            for (int i = 0; i < elementPrefabs.Count; i++)
            {
                GameObject prefab = elementPrefabs[i];
                if (prefab == null) continue;

                VR_EditableElement element = prefab.GetComponent<VR_EditableElement>();
                string type = element != null && !string.IsNullOrEmpty(element.elementType)
                    ? element.GetTypeName()
                    : VR_EditableElement.GetTypeForSourcePrefab(prefab.name);

                if (type == elementType)
                {
                    SelectElement(i);
                    return;
                }
            }

            Debug.LogWarning($"[VR-FP1-2] Aucun prefab de type '{elementType}' dans la palette.");
        }

        public void SelectNext()
        {
            if (elementPrefabs == null || elementPrefabs.Count == 0) return;
            SelectElement((SelectedIndex + 1) % elementPrefabs.Count);
        }

        public void SelectPrevious()
        {
            if (elementPrefabs == null || elementPrefabs.Count == 0) return;
            SelectElement((SelectedIndex - 1 + elementPrefabs.Count) % elementPrefabs.Count);
        }

        public GameObject SpawnSelected()
        {
            GameObject prefab = SelectedPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("[VR-FP1-2] Aucun prefab selectionne : impossible de creer l'element.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                ShowToast("Aucun element selectionne !", 2f);
                return null;
            }

            Vector3 position;
            Quaternion rotation;

            if (m_Camera == null) m_Camera = FindPlayerCamera();

            if (spawnInFrontOfHead && m_Camera != null)
            {
                Vector3 forward = m_Camera.transform.forward;
                Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
                if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
                flatForward.Normalize();

                position = m_Camera.transform.position + flatForward * spawnDistance;
                position.y = GetSafeSpawnHeight(position);

                rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            }
            else
            {
                position = spawnPoint != null ? spawnPoint.position : new Vector3(0f, spawnHeight, 0f);
                position.y = GetSafeSpawnHeight(position);
                rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            }

            GameObject created = Instantiate(prefab, position, rotation, levelRoot);
            created.name = prefab.name;

            VR_EditableElement editable = created.GetComponent<VR_EditableElement>();
            if (editable == null)
                editable = created.AddComponent<VR_EditableElement>();
            editable.ConfigureFromSourcePrefab(prefab.name);

            VRLevelEvents.RaiseElementSpawned(created);
            VRLevelEvents.RaiseFeedback(VRFeedbackType.Place);

            Debug.Log($"[VR-FP1-2] Element cree : {created.name} en {position}.");
            ShowToast("Element cree !", 1.5f);
            UpdatePaletteState();

            return created;
        }

        public void CreateSelected() => SpawnSelected();

        public void ClearLevel()
        {
            if (levelRoot == null) return;

            for (int i = levelRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(levelRoot.GetChild(i).gameObject);
            }

            UpdatePaletteState();
            ShowToast("Niveau vide.", 2f);
        }

        public float GetSafeSpawnHeight(Vector3 worldPosition)
        {
            float height = spawnHeight;
            Ray ray = new Ray(new Vector3(worldPosition.x, 100f, worldPosition.z), Vector3.down);
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Ignore))
                height = Mathf.Max(height, hit.point.y + spawnSurfaceClearance);

            foreach (Renderer renderer in FindObjectsOfType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !IsVisualGround(renderer.name)) continue;
                Bounds bounds = renderer.bounds;
                if (worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x &&
                    worldPosition.z >= bounds.min.z && worldPosition.z <= bounds.max.z)
                    height = Mathf.Max(height, bounds.max.y + spawnSurfaceClearance);
            }
            return height;
        }

        private bool IsVisualGround(string rendererName)
        {
            if (visualGroundKeywords == null) return false;
            string value = rendererName.ToLowerInvariant();
            foreach (string keyword in visualGroundKeywords)
                if (!string.IsNullOrEmpty(keyword) && value.Contains(keyword.ToLowerInvariant())) return true;
            return false;
        }

        private void UpdatePaletteState()
        {
            if (m_StatusText != null)
            {
                string label = SelectedPrefab != null ? GetPrefabLabel(SelectedPrefab, SelectedIndex) : "-";
                m_StatusText.text = $"Selection : {label}\nElements poses : {VR_EditableElement.All.Count}";
            }

            for (int i = 0; i < m_ElementButtons.Count; i++)
            {
                ElementButton entry = m_ElementButtons[i];
                if (entry.background == null) continue;
                entry.background.color = (entry.index == SelectedIndex) ? s_SelectedColor : s_ButtonColor;
            }
        }

        private static Button CreateButton(Transform parent, string label, Color color, float height)
        {
            GameObject go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;

            CreateText(go.transform, label, 20f, height);

            return button;
        }

        private static TMP_Text CreateText(Transform parent, string content, float fontSize, float height)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = height;

            return text;
        }
    }
}