// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VRLevelExporter.cs                 ->  fonctionnalite VR-FP1-5
//  ---------------------------------------------------------------------------
//  "Export JSON du niveau"
//
//  Le niveau est sauvegarde dans un fichier JSON au format impose par le
//  binome PC :
//
//      {
//          "levelName": "DemoScene_Forest",
//          "elements": [
//              {
//                  "type": "Platform_Classical",
//                  "position": { "x": 0, "y": 1, "z": 2 },
//                  "rotation": { "x": 0, "y": 90, "z": 0 },
//                  "scale":    { "x": 1, "y": 1, "z": 1 },
//                  "moveParams": null
//              },
//              {
//                  "type": "Platform_Move",
//                  "position": { ... },
//                  "rotation": { ... },
//                  "scale":    { ... },
//                  "moveParams": {
//                      "startPosition": { "x": 0, "y": 1, "z": 0 },
//                      "endPosition":   { "x": 0, "y": 1, "z": 8 },
//                      "duration": 3
//                  }
//              }
//          ]
//      }
//
//  - "rotation" est exprimee en degres (transform.eulerAngles) ;
//  - "moveParams" vaut null pour un element fixe ;
//  - le JSON est ecrit "a la main" (StringBuilder) pour garantir exactement
//    cette structure, y compris le null.
//
//  Fichiers produits :
//      * Application.persistentDataPath/<levelName>.json  (toujours)
//      * <projet>/LevelExports/<levelName>.json           (option, pratique
//         pour recuperer le fichier depuis le PC)
//
//  Declenchement : bouton "Exporter (JSON)" de la palette RV, touche F5, ou
//  bouton X de la manette gauche.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// VR-FP1-5 : export du niveau au format JSON.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRLevelExporter : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Configuration du fichier
        // ---------------------------------------------------------------------

        [Header("Fichier de sortie")]
        [Tooltip("Nom du niveau ecrit dans \"levelName\" et utilisé comme nom de fichier.")]
        public string levelName = "DemoScene_Forest";

        [Tooltip("Ecrire aussi une copie dans <projet>/LevelExports/.")]
        public bool exportToProjectFolder = true;

        // ---------------------------------------------------------------------
        // Entree
        // ---------------------------------------------------------------------

        [Header("Entree")]
        [Tooltip("Creer automatiquement l'action par defaut (bouton X, touche F5).")]
        public bool createDefaultInputAction = true;

        [Tooltip("Action personnalisee (optionnel) : remplace l'action par defaut.")]
        public InputActionProperty exportAction;

        [Header("Options")]
        public bool logExport = true;

        [Tooltip("Verifier types, transforms et hauteur avant ecriture du JSON.")]
        public bool validateElementsBeforeExport = true;

        [Tooltip("Hauteur minimale acceptable pour un element exporte.")]
        public float minimumElementHeight = -1f;

        /// <summary>Resume de la derniere validation affiche dans les logs/UI.</summary>
        public string LastValidationMessage { get; private set; }

        /// <summary>Chemin du dernier fichier exporte.</summary>
        public string LastExportPath { get; private set; }

        /// <summary>Nombre d'exports realises depuis le lancement.</summary>
        public int ExportCount { get; private set; }

        [Header("Confirmation d'ecrasement")]
        [Tooltip("Le premier export d'un nom deja existant demande une seconde validation.")]
        public bool requireOverwriteConfirmation = true;

        /// <summary>True lorsqu'un second clic sur Exporter confirmera l'ecrasement.</summary>
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

        // ---------------------------------------------------------------------
        // Cycle de vie Unity
        // ---------------------------------------------------------------------

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

        // ---------------------------------------------------------------------
        // Export
        // ---------------------------------------------------------------------

        /// <summary>Met a jour le nom de fichier depuis le champ de la palette RV.</summary>
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

        /// <summary>Exporte le niveau (appelable par un UnityEvent / bouton RV).</summary>
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
            }
            catch (Exception exception)
            {
                Debug.LogError($"[VR-FP1-5] Echec de l'export : {exception.Message}");
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Error);
            }
        }

        private static void WriteFile(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // UTF-8 sans BOM + retours a la ligne explicites ("\n") pour que le
            // fichier soit identique sous Windows et sous Linux/macOS.
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
            // Application.dataPath = <projet>/Assets
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        // ---------------------------------------------------------------------
        // Construction du JSON
        // ---------------------------------------------------------------------

        /// <summary>Construit le JSON avec JsonUtility et des modeles serialisables.</summary>
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

        /// <summary>Verifie les donnees avant ecriture : aucun element invalide n'est exporte par erreur.</summary>
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

        /// <summary>Liste triee des elements, pour un export stable et comparable.</summary>
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
