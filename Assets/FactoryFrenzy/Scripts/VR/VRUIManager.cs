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

        [Tooltip("Hauteur de creation (metres).")]
        public float spawnHeight = 1f;

        [Header("Palette RV")]
        [Tooltip("La palette suit la tete de l'utilisateur (recommande).")]
        public bool followHead = true;

        public float paletteDistance = 1.7f;
        public float paletteVerticalOffset = -0.15f;
        public Vector2 paletteSize = new Vector2(520f, 700f);
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

        private Canvas m_Canvas;
        private Transform m_CanvasTransform;
        private TMP_Text m_StatusText;
        private readonly List<Image> m_TypeButtonBackgrounds = new List<Image>();
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
            m_Camera = Camera.main;
            BuildPalette();
            SelectElement(0);
        }

        private void LateUpdate()
        {
            if (!followHead || m_CanvasTransform == null) return;

            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Camera == null) return;

            PlacePaletteInFrontOfCamera();
        }

        // ---------------------------------------------------------------------
        // Construction de la palette
        // ---------------------------------------------------------------------

        /// <summary>Construit le Canvas World Space et tous ses boutons.</summary>
        public void BuildPalette()
        {
            if (m_Canvas != null) return;

            GameObject canvasGo = new GameObject("VR_Palette");
            m_CanvasTransform = canvasGo.transform;
            m_CanvasTransform.localScale = Vector3.one * paletteScale;

            m_Canvas = canvasGo.AddComponent<Canvas>();
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

            // Un bouton par prefab d'element.
            for (int i = 0; i < elementPrefabs.Count; i++)
            {
                int index = i; // capture locale pour le lambda
                GameObject prefab = elementPrefabs[i];
                if (prefab == null) continue;

                Button button = CreateButton(canvasGo.transform, GetPrefabLabel(prefab, i), s_ButtonColor, 38f);
                button.onClick.AddListener(() => SelectElement(index));

                m_TypeButtonBackgrounds.Add(button.targetGraphic as Image);
            }

            // Boutons d'action.
            Button createButton = CreateButton(canvasGo.transform, "Creer", s_ActionColor, 42f);
            createButton.onClick.AddListener(CreateSelected);

            Button deleteButton = CreateButton(canvasGo.transform, "Supprimer", s_ActionColor, 42f);
            deleteButton.onClick.AddListener(() => onDeleteRequested.Invoke());

            Button exportButton = CreateButton(canvasGo.transform, "Exporter (JSON)", s_ActionColor, 42f);
            exportButton.onClick.AddListener(() => onExportRequested.Invoke());

            PlacePaletteInFrontOfCamera();
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

            m_CanvasTransform.position = origin + flatForward * paletteDistance + Vector3.up * paletteVerticalOffset;

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
                string type = element != null ? element.GetTypeName() : prefab.name;

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

            if (spawnInFrontOfHead && m_Camera != null)
            {
                Vector3 forward = m_Camera.transform.forward;
                Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
                if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
                flatForward.Normalize();

                position = m_Camera.transform.position + flatForward * spawnDistance;
                position.y = spawnHeight;

                rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            }
            else
            {
                position = spawnPoint != null ? spawnPoint.position : new Vector3(0f, spawnHeight, 0f);
                rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            }

            GameObject created = Instantiate(prefab, position, rotation, levelRoot);
            created.name = prefab.name;

            // Securite : un element cree doit toujours porter son composant.
            if (created.GetComponent<VR_EditableElement>() == null)
            {
                created.AddComponent<VR_EditableElement>();
            }

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

            for (int i = 0; i < m_TypeButtonBackgrounds.Count; i++)
            {
                Image image = m_TypeButtonBackgrounds[i];
                if (image == null) continue;

                image.color = (i == SelectedIndex) ? s_SelectedColor : s_ButtonColor;
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
