using System.Collections.Generic;
using FactoryFrenzy.PC;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le lobby (menu principal).
    /// Crée une scène "MainMenu.unity" avec :
    ///   - un Canvas + titre + bouton "Lancer la partie",
    ///   - le script MainMenu branché sur le bouton,
    ///   - un EventSystem compatible avec le nouvel Input System,
    ///   - la place en PREMIER dans les Build Settings (scène de démarrage).
    /// </summary>
    public static class PcMainMenuSetup
    {
        private const string MenuScenePath = "Assets/FactoryFrenzy/Scenes/MainMenu.unity";

        [MenuItem("Tools/Factory Frenzy/17) Créer le lobby (menu principal)", false, 17)]
        public static void CreateMainMenu()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            // --- Nouvelle scène -----------------------------------------------
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Caméra (fond sombre uniforme) ---------------------------------
            GameObject cameraGo = new GameObject("MainCamera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            // --- Canvas + titre + bouton ---------------------------------------
            GameObject canvasGo = new GameObject("MenuUI",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Titre.
            TMP_Text title = CreateText(canvas.transform, "Title", "Factory Frenzy", 110f, new Vector2(0f, 160f));

            // Bouton "Lancer la partie".
            GameObject buttonGo = new GameObject("PlayButton", typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(canvas.transform, false);
            Image buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.16f, 0.50f, 0.90f, 1f);
            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -80f);
            buttonRect.sizeDelta = new Vector2(420f, 96f);

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            CreateText(buttonGo.transform, "Label", "Lancer la partie", 42f, Vector2.zero);

            // --- Script MainMenu + câblage du bouton ---------------------------
            GameObject menuGo = new GameObject("MainMenu", typeof(MainMenu));
            MainMenu menu = menuGo.GetComponent<MainMenu>();

            // IMPORTANT : un simple AddListener() en mode Édition n'est PAS
            // sérialisé (le clic disparaît à la sauvegarde). Il faut un
            // "persistent listener" pour que le bouton fonctionne en Play.
            UnityEventTools.AddPersistentListener(button.onClick, menu.PlayGame);

            // --- EventSystem (nouvel Input System, indispensable pour cliquer) --
            GameObject eventSystemGo = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            // --- Sauvegarde de la scène ---------------------------------------
            EditorSceneManager.SaveScene(scene, MenuScenePath);

            // --- Build Settings : lobby en index 0 ----------------------------
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == MenuScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[FF-Repair] Lobby créé : " + MenuScenePath +
                      ". Il est en 1er dans les Build Settings. Appuie sur Play -> 'Lancer la partie'.");
        }

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
            rect.sizeDelta = new Vector2(1200f, 200f);
            return label;
        }
    }
}
