using FactoryFrenzy.PC;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-PLA-P2 (plateforme de départ + compte à rebours).
    /// Il prépare la scène SANS rien créer à la main :
    ///   - ajoute un objet "RaceManager" (script RaceManager),
    ///   - crée un Canvas "RaceUI" avec un grand texte de décompte,
    ///   - relie automatiquement le joueur et le texte au RaceManager,
    ///   - sauvegarde la scène.
    /// </summary>
    public static class PcRaceSetup
    {
        [MenuItem("Tools/Factory Frenzy/5) Setup PC-PLA-P2 (départ + countdown)", false, 5)]
        public static void SetupStartAndCountdown()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            // --- 1) Le joueur (ThirdPersonController du Starter Assets) ------------
            ThirdPersonController player = Object.FindObjectOfType<ThirdPersonController>(true);
            if (player == null)
            {
                Debug.LogError("[FF-Repair] Aucun 'ThirdPersonController' trouvé dans la scène. " +
                               "Lance d'abord '2) Réparer Game_Level_01 (PC-PLA-P1)'.");
                return;
            }

            // --- 2) Le RaceManager -------------------------------------------------
            RaceManager manager = Object.FindObjectOfType<RaceManager>(true);
            if (manager == null)
            {
                GameObject managerGo = new GameObject("RaceManager");
                Undo.RegisterCreatedObjectUndo(managerGo, "Créer RaceManager");
                manager = managerGo.AddComponent<RaceManager>();
                Debug.Log("[FF-Repair] Objet 'RaceManager' créé.");
            }

            // --- 3) L'UI de décompte (Canvas + texte) -----------------------------
            TMP_Text label = Object.FindObjectOfType<TMP_Text>(true);

            if (label == null)
            {
                GameObject canvasGo = new GameObject("RaceUI",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Créer RaceUI");

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // au-dessus du reste de l'UI

                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

                GameObject textGo = new GameObject("CountdownText", typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(textGo, "Créer CountdownText");
                textGo.transform.SetParent(canvasGo.transform, false);

                label = textGo.GetComponent<TMP_Text>();
                label.text = string.Empty;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 160f;
                label.fontStyle = FontStyles.Bold;
                label.color = Color.white;

                // Police par défaut de TextMesh Pro (celle des "TMP Essentials").
                if (TMP_Settings.defaultFontAsset != null)
                {
                    label.font = TMP_Settings.defaultFontAsset;
                }

                // On centre le texte au milieu de l'écran.
                RectTransform rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1000f, 400f);

                Debug.Log("[FF-Repair] Canvas 'RaceUI' + texte de décompte créés.");
            }

            // --- 4) Câblage automatique des références ----------------------------
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("playerController").objectReferenceValue = player;
            serializedManager.FindProperty("countdownText").objectReferenceValue = label;
            serializedManager.ApplyModifiedProperties();

            // --- 5) Sauvegarde -----------------------------------------------------
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[FF-Repair] PC-PLA-P2 prêt : RaceManager + UI décompte configurés. " +
                      "Appuie sur Play pour voir 3...2...1...GO !");
        }
    }
}
