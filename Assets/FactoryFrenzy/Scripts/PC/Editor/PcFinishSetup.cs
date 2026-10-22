using System.Collections.Generic;
using FactoryFrenzy.PC;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-PLA-P3 (plateforme d'arrivée + écran de résultats).
    /// Il prépare la scène sans rien créer à la main :
    ///   - récupère (ou place) la "Platform End",
    ///   - crée un volume trigger "FinishTrigger" (script FinishPlatform),
    ///   - construit le panneau de résultats (Canvas + textes + bouton),
    ///   - câble le tout au RaceManager,
    ///   - ajoute la scène aux Build Settings (nécessaire pour le bouton "Recommencer").
    /// </summary>
    public static class PcFinishSetup
    {
        private const string EndPrefabPath =
            "Assets/FactoryFrenzy/Prefabs/Platform and Trap/Platform End.prefab";

        [MenuItem("Tools/Factory Frenzy/6) Setup PC-PLA-P3 (arrivée + résultats)", false, 6)]
        public static void SetupFinishAndResults()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            // --- 1) Le RaceManager doit déjà exister (créé par le menu 5) --------
            RaceManager manager = Object.FindObjectOfType<RaceManager>(true);
            if (manager == null)
            {
                Debug.LogError("[FF-Repair] Aucun 'RaceManager' trouvé. Lance d'abord " +
                               "'5) Setup PC-PLA-P2 (départ + countdown)'.");
                return;
            }

            // --- 2) La plateforme d'arrivée ---------------------------------------
            GameObject endPlatform = FindByName("Platform End");
            GameObject startPlatform = FindByName("Platform Start");

            if (endPlatform == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EndPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError("[FF-Repair] Prefab introuvable : " + EndPrefabPath);
                    return;
                }
                endPlatform = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                endPlatform.name = "Platform End";
                Debug.Log("[FF-Repair] Plateforme d'arrivée instanciée.");
            }

            // Niveau encore en travaux : si l'arrivée est très loin du départ,
            // on la colle à côté du départ pour rendre la course testable tout de suite.
            if (startPlatform != null &&
                Vector3.Distance(endPlatform.transform.position, startPlatform.transform.position) > 30f)
            {
                PlaceEndBesideStart(startPlatform, endPlatform);
                Debug.Log("[FF-Repair] Plateforme d'arrivée rapprochée du départ (niveau testable).");
            }

            // --- 3) Le volume de détection (trigger) ------------------------------
            Transform existingTrigger = endPlatform.transform.Find("FinishTrigger");
            GameObject trigger;
            if (existingTrigger == null)
            {
                trigger = new GameObject("FinishTrigger");
                Undo.RegisterCreatedObjectUndo(trigger, "Créer FinishTrigger");
                trigger.transform.SetParent(endPlatform.transform, false);

                // On dimensionne le trigger d'après la taille visuelle de la plateforme.
                Renderer[] renderers = endPlatform.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    trigger.transform.position = bounds.center + Vector3.up * 0.5f;

                    BoxCollider box = Undo.AddComponent<BoxCollider>(trigger);
                    box.isTrigger = true;
                    box.size = bounds.size + new Vector3(1.0f, 2.0f, 1.0f); // marge généreuse
                }
                else
                {
                    BoxCollider box = Undo.AddComponent<BoxCollider>(trigger);
                    box.isTrigger = true;
                    box.size = new Vector3(2f, 3f, 2f);
                }

                trigger.AddComponent<FinishPlatform>();
                Debug.Log("[FF-Repair] Volume d'arrivée 'FinishTrigger' créé.");
            }
            else
            {
                trigger = existingTrigger.gameObject;
                if (trigger.GetComponent<FinishPlatform>() == null) trigger.AddComponent<FinishPlatform>();
            }

            // --- 4) L'écran de résultats ------------------------------------------
            Canvas canvas = Object.FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("RaceUI",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Créer RaceUI");
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            }

            GameObject panel = FindByName("ResultsPanel");
            if (panel == null)
            {
                // Fond sombre plein écran.
                panel = new GameObject("ResultsPanel", typeof(Image));
                Undo.RegisterCreatedObjectUndo(panel, "Créer ResultsPanel");
                panel.transform.SetParent(canvas.transform, false);
                Image background = panel.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.78f);
                RectTransform prt = panel.GetComponent<RectTransform>();
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero;
                prt.offsetMax = Vector2.zero;

                CreateText(panel.transform, "Title", "Course terminée !", 72f, new Vector2(0f, 180f));
                CreateText(panel.transform, "TimeText", "Temps : 0.00 s", 64f, new Vector2(0f, 40f));

                // Bouton "Recommencer".
                GameObject buttonGo = new GameObject("RestartButton", typeof(Image), typeof(Button));
                buttonGo.transform.SetParent(panel.transform, false);
                Image buttonImage = buttonGo.GetComponent<Image>();
                buttonImage.color = new Color(0.16f, 0.50f, 0.90f, 1f);
                RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(0f, -120f);
                buttonRect.sizeDelta = new Vector2(320f, 80f);
                Button button = buttonGo.GetComponent<Button>();
                button.targetGraphic = buttonImage;
                CreateText(buttonGo.transform, "Label", "Recommencer", 36f, Vector2.zero);

                Debug.Log("[FF-Repair] Écran de résultats créé.");
            }

            // --- 5) Câblage de RaceResultsUI (sur le RaceManager, toujours actif) -
            RaceResultsUI ui = manager.gameObject.GetComponent<RaceResultsUI>();
            if (ui == null) ui = Undo.AddComponent<RaceResultsUI>(manager.gameObject);

            TMP_Text timeText = panel.transform.Find("TimeText").GetComponent<TMP_Text>();
            Button restartButton = panel.transform.Find("RestartButton").GetComponent<Button>();

            SerializedObject serializedUi = new SerializedObject(ui);
            serializedUi.FindProperty("panel").objectReferenceValue = panel;
            serializedUi.FindProperty("timeText").objectReferenceValue = timeText;
            serializedUi.FindProperty("restartButton").objectReferenceValue = restartButton;
            serializedUi.ApplyModifiedProperties();

            panel.SetActive(false); // masqué au démarrage

            // --- 5b) EventSystem + module du NOUVEL Input System (clics UI) -------
            // Le projet utilise le nouvel Input System (Starter Assets) : c'est donc
            // InputSystemUIInputModule qu'il faut, pas l'ancien StandaloneInputModule.
            EventSystem eventSystem = Object.FindObjectOfType<EventSystem>(true);
            if (eventSystem == null)
            {
                GameObject eventSystemGo = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Créer EventSystem");
                eventSystem = eventSystemGo.GetComponent<EventSystem>();
            }

            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                if (legacyModule != null) Undo.DestroyObjectImmediate(legacyModule);
                inputModule = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            }
            inputModule.AssignDefaultActions(); // branche les actions UI par défaut (clic, pointeur...)
            Debug.Log("[FF-Repair] EventSystem configuré (InputSystemUIInputModule) : clic UI fonctionnel.");

            // --- 6) Ajouter la scène aux Build Settings (pour "Recommencer") ------
            Scene scene = SceneManager.GetActiveScene();
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scene.path))
            {
                scenes.Add(new EditorBuildSettingsScene(scene.path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[FF-Repair] Scène ajoutée aux Build Settings.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[FF-Repair] PC-PLA-P3 prêt : arrivée + écran de résultats configurés. " +
                      "Play, va sur la plateforme bleue -> écran de résultats + bouton Recommencer.");
        }

        // ---------------------------------------------------------------------
        // Utilitaires
        // ---------------------------------------------------------------------

        /// <summary>Crée un texte TextMeshPro centré et renvoie son composant.</summary>
        private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            TMP_Text label = go.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(900f, 160f);
            return label;
        }

        /// <summary>Cherche un GameObject par nom exact (actifs et inactifs).</summary>
        private static GameObject FindByName(string name)
        {
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go.name == name) return go;
            }
            return null;
        }

        /// <summary>Boîte englobante monde d'un objet (via ses Renderers).</summary>
        private static Bounds GetWorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Place l'arrivée juste à côté du départ (celui de gauche), même hauteur.</summary>
        private static void PlaceEndBesideStart(GameObject start, GameObject end)
        {
            Bounds startBounds = GetWorldBounds(start);
            Bounds endBounds = GetWorldBounds(end);

            Vector3 delta = new Vector3(
                (startBounds.max.x + 0.3f) - endBounds.min.x, // collé à droite du départ
                startBounds.center.y - endBounds.center.y,    // même hauteur
                startBounds.center.z - endBounds.center.z);   // aligné en profondeur

            end.transform.position += delta;
        }
    }
}
