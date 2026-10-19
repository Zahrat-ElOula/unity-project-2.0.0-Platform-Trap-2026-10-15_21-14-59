using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.VR
{
    [DisallowMultipleComponent]
    public class VRLevelExporter : MonoBehaviour
    {
        [Header("Fichier de sortie")]
        public string levelName = "DemoScene_Forest";
        public bool exportToProjectFolder = true;

        [Header("Entree")]
        public bool createDefaultInputAction = true;
        public InputActionProperty exportAction;

        [Header("Options")]
        public bool logExport = true;
        public bool validateElementsBeforeExport = true;
        public float minimumElementHeight = -1f;

        public string LastValidationMessage { get; private set; }
        public string LastExportPath { get; private set; }
        public int ExportCount { get; private set; }

        [Header("Confirmation d'ecrasement")]
        public bool requireOverwriteConfirmation = true;
        public bool IsAwaitingOverwriteConfirmation { get; private set; }

        private string m_PendingOverwriteName;

        [Serializable]
        private class LevelData
        {
            public string levelName;
            public List<ElementData> elements = new List<ElementData>();
        }

        [Serializable]
        private class ElementData
        {
            public string type;
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 scale;
            public MoveData moveParams;
        }

        [Serializable]
        private class MoveData
        {
            public Vector3 startPosition;
            public Vector3 endPosition;
            public float duration;
        }

        private InputAction m_BuiltInAction;

        private void OnEnable()
        {
            SetupInput();
        }

        private void OnDisable()
        {
            if (m_BuiltInAction != null)
            {
                m_BuiltInAction.performed -= OnExportPerformed;
                m_BuiltInAction.Disable();
                m_BuiltInAction.Dispose();
                m_BuiltInAction = null;
            }

            if (exportAction.action != null)
            {
                exportAction.action.performed -= OnExportPerformed;
            }
        }

        private void SetupInput()
        {
            if (exportAction.action != null)
            {
                exportAction.action.Enable();
                exportAction.action.performed += OnExportPerformed;
                return;
            }

            if (!createDefaultInputAction) return;

            m_BuiltInAction = new InputAction("VR_Export", InputActionType.Button);
            m_BuiltInAction.AddBinding("<XRController>{LeftHand}/secondaryButton"); // bouton X
            m_BuiltInAction.AddBinding("<Keyboard>/f5");
            m_BuiltInAction.AddBinding("<Keyboard>/e");
            m_BuiltInAction.performed += OnExportPerformed;
            m_BuiltInAction.Enable();
        }

        private void OnExportPerformed(InputAction.CallbackContext context)
        {
            ExportLevel();
        }

        public void SetLevelName(string value)
        {
            string cleaned = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (cleaned.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - 5);

            foreach (char invalid in Path.GetInvalidFileNameChars())
                cleaned = cleaned.Replace(invalid.ToString(), string.Empty);

            levelName = cleaned;
            IsAwaitingOverwriteConfirmation = false;
            m_PendingOverwriteName = null;
        }

        public void ExportLevel()
        {
            try
            {
                string name = ResolveLevelName();
                List<VR_EditableElement> elements = CollectElements();
                if (validateElementsBeforeExport && !ValidateElements(elements, out string validationMessage))
                {
                    LastValidationMessage = validationMessage;
                    Debug.LogError("[VR-FP1-5] Export annule : " + validationMessage);
                    VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                    FindObjectOfType<VRUIManager>()?.ShowToast("Erreur : " + validationMessage, 3f);
                    return;
                }
                LastValidationMessage = $"Validation OK : {elements.Count} element(s).";
                string mainPath = Path.Combine(Application.persistentDataPath, name + ".json");
                string projectPath = exportToProjectFolder
                    ? Path.Combine(GetProjectRoot(), "LevelExports", name + ".json")
                    : null;

                bool exists = File.Exists(mainPath) || (projectPath != null && File.Exists(projectPath));
                if (requireOverwriteConfirmation && exists &&
                    (!IsAwaitingOverwriteConfirmation || m_PendingOverwriteName != name))
                {
                    IsAwaitingOverwriteConfirmation = true;
                    m_PendingOverwriteName = name;
                    Debug.LogWarning($"[VR-FP1-5] Le fichier '{name}.json' existe deja. Appuyez une seconde fois sur Exporter pour confirmer l'ecrasement.");
                    VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                    FindObjectOfType<VRUIManager>()?.ShowToast("Fichier existant. Re-cliquez pour ecraser.", 3f);
                    return;
                }

                string json = BuildJson(name, elements);
                WriteFile(mainPath, json);
                if (projectPath != null) WriteFile(projectPath, json);

                IsAwaitingOverwriteConfirmation = false;
                m_PendingOverwriteName = null;
                ExportCount++;
                LastExportPath = mainPath;
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Save);
                VRLevelEvents.RaiseLevelExported(mainPath);

                if (logExport)
                    Debug.Log($"[VR-FP1-5] {LastValidationMessage} Niveau exporte : {elements.Count} elements -> {mainPath}" +
                              (projectPath != null ? $"\n[VR-FP1-5] Copie projet : {projectPath}" : string.Empty));

                FindObjectOfType<VRUIManager>()?.ShowToast("Export JSON reussi !", 3f);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[VR-FP1-5] Echec de l'export : {exception.Message}");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
                FindObjectOfType<VRUIManager>()?.ShowToast("Echec de l'export.", 3f);
            }
        }

        private static void WriteFile(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private string ResolveLevelName()
        {
            if (!string.IsNullOrEmpty(levelName)) return levelName;
            Scene scene = SceneManager.GetActiveScene();
            return string.IsNullOrEmpty(scene.name) ? "niveau" : scene.name;
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        public string BuildJson(string levelNameValue)
        {
            return BuildJson(levelNameValue, CollectElements());
        }

        private string BuildJson(string levelNameValue, List<VR_EditableElement> elements)
        {
            LevelData data = new LevelData { levelName = levelNameValue };
            foreach (VR_EditableElement element in elements)
            {
                Transform transform = element.transform;
                ElementData item = new ElementData
                {
                    type = element.GetTypeName(),
                    position = transform.position,
                    rotation = transform.eulerAngles,
                    scale = transform.localScale,
                    moveParams = element.hasMovement && element.duration > 0f
                        ? new MoveData
                        {
                            startPosition = element.WorldMoveStart,
                            endPosition = element.WorldMoveEnd,
                            duration = element.duration
                        }
                        : null
                };
                data.elements.Add(item);
            }
            return JsonUtility.ToJson(data, true);
        }

        private bool ValidateElements(List<VR_EditableElement> elements, out string message)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                VR_EditableElement element = elements[i];
                if (element == null) continue;
                Transform transform = element.transform;
                if (string.IsNullOrWhiteSpace(element.GetTypeName()))
                {
                    message = $"L'element {i + 1} n'a pas de type.";
                    return false;
                }
                if (!IsFinite(transform.position) || !IsFinite(transform.eulerAngles) || !IsFinite(transform.localScale))
                {
                    message = $"'{element.name}' contient une position, rotation ou echelle invalide.";
                    return false;
                }
                if (transform.localScale.x <= 0f || transform.localScale.y <= 0f || transform.localScale.z <= 0f)
                {
                    message = $"'{element.name}' a une echelle nulle ou negative.";
                    return false;
                }
                if (transform.position.y < minimumElementHeight)
                {
                    message = $"'{element.name}' est sous la hauteur minimale ({minimumElementHeight:0.##} m).";
                    return false;
                }
            }
            message = $"Validation OK : {elements.Count} element(s).";
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        private static List<VR_EditableElement> CollectElements()
        {
            List<VR_EditableElement> elements = new List<VR_EditableElement>();
            foreach (VR_EditableElement element in VR_EditableElement.All)
            {
                if (element != null) elements.Add(element);
            }
            elements.Sort((a, b) =>
            {
                int byType = string.CompareOrdinal(a.GetTypeName(), b.GetTypeName());
                return byType != 0 ? byType : string.CompareOrdinal(a.name, b.name);
            });
            return elements;
        }
    }
}