// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VRUIManager.cs                     ->  fonctionnalite VR-FP1-2
//  ---------------------------------------------------------------------------
//  "Selectionner et creer des elements dans une UI"
//
//  Ce gestionnaire construit au demarrage une palette RV (Canvas en World
//  Space + TrackedDeviceGraphicRaycaster) placee devant l'utilisateur :
//
//      [ Platform_Classical ]   <- un bouton par prefab de la liste elementPrefabs
//      [ Platform_Move      ]
//      [ Trap_Fan           ]
//      ...
//      [ Creer ]  [ Supprimer ]  [ Exporter ]
//
//  - cliquer un bouton de type selectionne l'element a construire ;
//  - "Creer" instancie le prefab selectionne devant l'utilisateur, dans le
//    LevelRoot, puis previent les autres gestionnaires (VRLevelEvents) ;
//  - "Supprimer" et "Exporter" declenchent les UnityEvent onDeleteRequested et
//    onExportRequested : la scene les relie a VRDeleteManager (VR-FP1-4) et a
//    VRLevelExporter (VR-FP1-5).
//
//  L'interface est generee par code afin de ne jamais casser quand la liste de
//  prefabs change : ajouter un prefab dans elementPrefabs ajoute un bouton.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// VR-FP1-2 : selection et creation d'elements via une interface RV.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRUIManager : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Elements disponibles
        // ---------------------------------------------------------------------

        [Header("Prefabs disponibles (Assets/FactoryFrenzy/Prefabs_VR_Editor)")]
        [Tooltip("Chaque prefab de cette liste devient un bouton dans la palette RV.")]
        public List<GameObject> elementPrefabs = new List<GameObject>();

        [Tooltip("Libelles personnalises (optionnel, meme ordre que elementPrefabs).")]
        public string[] elementLabels;

        // ---------------------------------------------------------------------
        // References de scene
        // ---------------------------------------------------------------------

        [Header("References de scene")]
        [Tooltip("Parent des elements crees (Level_Elements).")]
        public Transform levelRoot;

        [Tooltip("Position de repli si la camera n'est pas trouvee.")]
        public Transform spawnPoint;

        [Header("Creation")]
        [Tooltip("Creer l'element devant la tete de l'utilisateur.")]
        public bool spawnInFrontOfHead = true;

        [Tooltip("Distance de creation devant la tete (metres).")]
        public float spawnDistance = 1.3f;

        [Tooltip("Hauteur de creation minimale (metres).")]
        public float spawnHeight = 1f;

        [Tooltip("Espace laisse au-dessus du decor/sol detecte.")]
        [Min(0.01f)] public float spawnSurfaceClearance = 0.08f;

        [Tooltip("Noms de rendu consideres comme sol visuel.")]
        public string[] visualGroundKeywords = { "ground", "dirt", "terrain", "floor", "land" };

        [Header("Palette RV")]
        [Tooltip("La palette suit la tete de l'utilisateur (recommande).")]
        public bool followHead = true;

        public float paletteDistance = 1.7f;
        public float paletteVerticalOffset = -0.08f;
        [Tooltip("Decalage lateral par rapport au regard. Negatif = a gauche, pour garder la vue libre.")]
        public float paletteHorizontalOffset = -0.8f;
        public Vector2 paletteSize = new Vector2(560f, 760f);
        public float paletteScale = 0.0012f;

        // ---------------------------------------------------------------------
        // Evenements (relies dans la scene aux autres fonctionnalites)
        // ---------------------------------------------------------------------

        [Header("Evenements de la palette")]
        public UnityEvent onDeleteRequested = new UnityEvent();
        public UnityEvent onExportRequested = new UnityEvent();

        // ---------------------------------------------------------------------
        // Etat
        // ---------------------------------------------------------------------

        /// <summary>Index du prefab actuellement selectionne dans la palette.</summary>
        public int SelectedIndex { get; private set; }

        /// <summary>Prefab actuellement selectionne (peut etre null).</summary>
        public GameObject SelectedPrefab =>
            (elementPrefabs != null && SelectedIndex >= 0 && SelectedIndex < elementPrefabs.Count)
                ? elementPrefabs[SelectedIndex]
                : null;

        /// <summary>Catégorie actuellement affichée pour réduire le temps de sélection.</summary>
        public ElementCategory ActiveCategory { get; private set; } = ElementCategory.Platforms;

        public enum ElementCategory
        {
            Platforms,
            Traps
        }

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
        private TMP_InputField m_LevelNameInput;
        private readonly List<ElementButton> m_ElementButtons = new List<ElementButton>();
        private readonly Dictionary<ElementCategory, Image> m_CategoryButtonBackgrounds = new Dictionary<ElementCategory, Image>();
        private Camera m_Camera;

        private static readonly Color s_ButtonColor = new Color(0.16f, 0.18f, 0.22f, 0.95f);
        private static readonly Color s_SelectedColor = new Color(0.95f, 0.55f, 0.10f, 1f);
        private static readonly Color s_ActionColor = new Color(0.20f, 0.35f, 0.55f, 0.95f);
        private static readonly Color s_TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        // ---------------------------------------------------------------------
        // Cycle de vie Unity
        // ---------------------------------------------------------------------

        private void Start()
        {
            m_Camera = FindPlayerCamera();
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

        private static Camera FindPlayerCamera()
        {
            Camera main = Camera.main;
            if (main != null) return main;

            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera camera in cameras)
                if (camera.enabled && camera.gameObject.activeInHierarchy) return camera;
            return cameras.Length > 0 ? cameras[0] : null;
        }

        // ---------------------------------------------------------------------
        // Construction de la palette
        // ---------------------------------------------------------------------

        /// <summary>Construit le Canvas World Space et tous ses boutons.</summary>
        public void BuildPalette()
        {
            if (m_Canvas != null) return;

            GameObject canvasGo = new GameObject("VR_Palette");
            m_Canvas = canvasGo.AddComponent<Canvas>();
            // Canvas remplace le Transform par un RectTransform : conserver la reference ensuite.
            m_CanvasTransform = canvasGo.transform;
            m_CanvasTransform.localScale = Vector3.one * paletteScale;
            m_Canvas.renderMode = RenderMode.WorldSpace;
            m_Canvas.worldCamera = m_Camera;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = paletteSize;

            // Permet aux rayons XR (manettes) d'interagir avec l'interface.
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            // Fond de la palette.
            Image background = canvasGo.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            background.raycastTarget = false;

            VerticalLayoutGroup layout = canvasGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // Ligne d'etat en haut de la palette.
            m_StatusText = CreateText(canvasGo.transform, "Selection : -", 20f, 34f);

            // Choix rapide par catégories : seules les plateformes OU les pièges
            // sont affichés. La palette reste ainsi courte et lisible en RV.
            GameObject categories = new GameObject("Categories", typeof(RectTransform));
            categories.transform.SetParent(canvasGo.transform, false);
            HorizontalLayoutGroup categoryLayout = categories.AddComponent<HorizontalLayoutGroup>();
            categoryLayout.spacing = 6f;
            categoryLayout.childForceExpandWidth = true;
            categoryLayout.childForceExpandHeight = false;
            LayoutElement categoryElement = categories.AddComponent<LayoutElement>();
            categoryElement.preferredHeight = 40f;
            categoryElement.minHeight = 40f;
            AddCategoryButton(categories.transform, "PLATEFORMES", ElementCategory.Platforms, new Color(0.10f, 0.42f, 0.80f, 0.98f));
            AddCategoryButton(categories.transform, "PIEGES", ElementCategory.Traps, new Color(0.75f, 0.22f, 0.12f, 0.98f));

            // Nom du fichier JSON : saisissable au clavier avec le simulateur
            // ou avec le clavier virtuel du casque.
            m_LevelNameInput = CreateInputField(canvasGo.transform, "Nom du niveau", "DemoScene_Forest", 38f);
            m_LevelNameInput.onValueChanged.AddListener(SetExportFileName);
            SetExportFileName(m_LevelNameInput.text);

            // Un bouton par prefab d'element.
            for (int i = 0; i < elementPrefabs.Count; i++)
            {
                int index = i; // capture locale pour le lambda
                GameObject prefab = elementPrefabs[i];
                if (prefab == null) continue;

                Button button = CreateButton(canvasGo.transform, GetPrefabLabel(prefab, i), s_ButtonColor, 38f);
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

            // Boutons d'action.
            Button createButton = CreateButton(canvasGo.transform, "CREER L'ELEMENT", s_ActionColor, 46f);
            createButton.onClick.AddListener(CreateSelected);

            Button deleteButton = CreateButton(canvasGo.transform, "SUPPRIMER SELECTION", new Color(0.65f, 0.16f, 0.14f, 0.98f), 42f);
            deleteButton.onClick.AddListener(() => onDeleteRequested.Invoke());

            Button exportButton = CreateButton(canvasGo.transform, "EXPORTER JSON", new Color(0.30f, 0.28f, 0.65f, 0.98f), 42f);
            exportButton.onClick.AddListener(() => onExportRequested.Invoke());

            CreateText(canvasGo.transform, "A : selection multiple | B : supprimer | X/F5 : exporter", 13f, 26f);
            SetCategory(ActiveCategory, false);
            PlacePaletteInFrontOfCamera();
        }

        private void AddCategoryButton(Transform parent, string label, ElementCategory category, Color color)
        {
            Button button = CreateButton(parent, label, color, 40f);
            button.onClick.AddListener(() => SetCategory(category));
            m_CategoryButtonBackgrounds[category] = button.targetGraphic as Image;
        }

        public void SetCategory(ElementCategory category)
        {
            SetCategory(category, true);
        }

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

        /// <summary>Met a jour le nom utilise par l'export JSON.</summary>
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

            // On ignore l'inclinaison de la tete pour garder la palette verticale.
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 right = m_Camera != null ? m_Camera.transform.right : Vector3.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
            m_CanvasTransform.position = origin + flatForward * paletteDistance +
                                         right * paletteHorizontalOffset + Vector3.up * paletteVerticalOffset;

            // Le Canvas (axe +Z) est tourne vers la camera pour rester lisible.
            Vector3 lookDirection = origin - m_CanvasTransform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                m_CanvasTransform.rotation = Quaternion.LookRotation(-lookDirection.normalized, Vector3.up);
            }
        }

        // ---------------------------------------------------------------------
        // Selection / creation
        // ---------------------------------------------------------------------

        /// <summary>Selectionne un prefab par son index dans elementPrefabs.</summary>
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

        /// <summary>Selectionne un prefab par son type logique (elementType).</summary>
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

        /// <summary>Selectionne le type suivant (bouton / action de manette).</summary>
        public void SelectNext()
        {
            if (elementPrefabs == null || elementPrefabs.Count == 0) return;
            SelectElement((SelectedIndex + 1) % elementPrefabs.Count);
        }

        /// <summary>Selectionne le type precedent.</summary>
        public void SelectPrevious()
        {
            if (elementPrefabs == null || elementPrefabs.Count == 0) return;
            SelectElement((SelectedIndex - 1 + elementPrefabs.Count) % elementPrefabs.Count);
        }

        /// <summary>
        /// Cree le prefab selectionne devant l'utilisateur et le place sous le
        /// LevelRoot. Renvoie l'objet cree (ou null).
        /// </summary>
        public GameObject SpawnSelected()        {
            GameObject prefab = SelectedPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("[VR-FP1-2] Aucun prefab selectionne : impossible de creer l'element.");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
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

            // Le prefab source est commun a l'editeur RV et au jeu PC. Les
            // composants propres a l'edition sont ajoutes uniquement a
            // l'instance creee, jamais a l'asset du binome.
            VR_EditableElement editable = created.GetComponent<VR_EditableElement>();
            if (editable == null)
                editable = created.AddComponent<VR_EditableElement>();
            editable.ConfigureFromSourcePrefab(prefab.name);

            // Prevenir les autres fonctionnalites (VRGrabManager, ...).
            VRLevelEvents.RaiseElementSpawned(created);
            VRLevelEvents.RaiseFeedback(VRFeedbackType.Place);

            Debug.Log($"[VR-FP1-2] Element cree : {created.name} en {position}.");
            UpdatePaletteState();

            return created;
        }

        /// <summary>
        /// Version void de SpawnSelected() : utilisable dans un bouton / UnityEvent.
        /// </summary>
        public void CreateSelected()
        {
            SpawnSelected();
        }

        /// <summary>Supprime tous les elements crees (remise a zero du niveau).</summary>
        public void ClearLevel()
        {
            if (levelRoot == null) return;

            for (int i = levelRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(levelRoot.GetChild(i).gameObject);
            }

            UpdatePaletteState();
        }

        /// <summary>Hauteur de creation au-dessus du sol physique et du decor forestier visible.</summary>
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

        // ---------------------------------------------------------------------
        // Mise a jour visuelle de la palette
        // ---------------------------------------------------------------------

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

        // ---------------------------------------------------------------------
        // Fabrique de widgets uGUI / TextMeshPro
        // ---------------------------------------------------------------------

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
