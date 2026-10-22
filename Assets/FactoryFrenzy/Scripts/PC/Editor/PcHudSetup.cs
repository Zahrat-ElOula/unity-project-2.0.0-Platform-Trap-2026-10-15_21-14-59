using FactoryFrenzy.PC;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur : ajoute un chrono HUD (haut de l'écran) qui affiche le temps
    /// de la course en direct. Crée le texte, le câble au script RaceTimerUI,
    /// puis sauvegarde la scène. (Menu 7)
    /// </summary>
    public static class PcHudSetup
    {
        [MenuItem("Tools/Factory Frenzy/7) Ajouter le chrono (HUD)", false, 7)]
        public static void AddRaceTimerHud()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            // Le RaceManager doit exister (menus 5/6 déjà passés).
            RaceManager manager = Object.FindObjectOfType<RaceManager>(true);
            if (manager == null)
            {
                Debug.LogError("[FF-Repair] Aucun 'RaceManager' trouvé. Lance d'abord le menu 5.");
                return;
            }

            // Canvas existant, sinon on en crée un.
            Canvas canvas = Object.FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("RaceUI",
                    typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Créer RaceUI");
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            }

            // Le texte du chrono (en haut au centre).
            Transform existing = canvas.transform.Find("TimerText");
            TMP_Text timer = existing != null ? existing.GetComponent<TMP_Text>() : null;

            if (timer == null)
            {
                GameObject go = new GameObject("TimerText", typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(go, "Créer TimerText");
                go.transform.SetParent(canvas.transform, false);

                timer = go.GetComponent<TMP_Text>();
                timer.text = "Temps : 0.00";
                timer.fontSize = 52f;
                timer.alignment = TextAlignmentOptions.Top;
                timer.color = Color.white;
                if (TMP_Settings.defaultFontAsset != null) timer.font = TMP_Settings.defaultFontAsset;

                RectTransform rect = timer.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -30f);
                rect.sizeDelta = new Vector2(700f, 100f);

                Debug.Log("[FF-Repair] Texte 'TimerText' créé.");
            }

            // Script RaceTimerUI (sur le RaceManager, objet toujours actif).
            RaceTimerUI ui = manager.gameObject.GetComponent<RaceTimerUI>();
            if (ui == null) ui = Undo.AddComponent<RaceTimerUI>(manager.gameObject);

            SerializedObject serializedUi = new SerializedObject(ui);
            serializedUi.FindProperty("timerText").objectReferenceValue = timer;
            serializedUi.ApplyModifiedProperties();

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[FF-Repair] Chrono HUD configuré. Play : le temps s'affiche en haut et défile.");
        }
    }
}
