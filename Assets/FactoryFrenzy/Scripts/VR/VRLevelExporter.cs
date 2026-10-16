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
using System.Globalization;
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

        /// <summary>Chemin du dernier fichier exporte.</summary>
        public string LastExportPath { get; private set; }

        /// <summary>Nombre d'exports realises depuis le lancement.</summary>
        public int ExportCount { get; private set; }

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

        /// <summary>Exporte le niveau (appelable par un UnityEvent / bouton RV).</summary>
        public void ExportLevel()
        {
            try
            {
                string name = ResolveLevelName();
                string json = BuildJson(name);

                // 1. Fichier principal (fonctionne sur PC comme sur Quest).
                string mainPath = Path.Combine(Application.persistentDataPath, name + ".json");
                WriteFile(mainPath, json);

                // 2. Copie dans le projet (pratique pour recuperer le fichier).
                string projectPath = null;
                if (exportToProjectFolder)
                {
                    projectPath = Path.Combine(GetProjectRoot(), "LevelExports", name + ".json");
                    WriteFile(projectPath, json);
                }

                ExportCount++;
                LastExportPath = mainPath;

                // Retour sonore + vibration de sauvegarde.
                VRLevelEvents.RaiseFeedback(VRFeedbackType.Save);

                if (logExport)
                {
                    Debug.Log($"[VR-FP1-5] Niveau exporte : {VR_EditableElement.All.Count} elements -> {mainPath}" +
                              (projectPath != null ? $"\n[VR-FP1-5] Copie projet : {projectPath}" : string.Empty));
                }
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

        /// <summary>Construit le contenu JSON complet du niveau.</summary>
        public string BuildJson(string levelNameValue)
        {
            List<VR_EditableElement> elements = CollectElements();

            StringBuilder builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append("    \"levelName\": \"").Append(Escape(levelNameValue)).Append("\",\n");
            builder.Append("    \"elements\": [\n");

            for (int i = 0; i < elements.Count; i++)
            {
                VR_EditableElement element = elements[i];
                Transform transform = element.transform;

                builder.Append("        {\n");
                builder.Append("            \"type\": \"").Append(Escape(element.GetTypeName())).Append("\",\n");

                AppendVector(builder, 12, "position", transform.position, true);
                AppendVector(builder, 12, "rotation", transform.eulerAngles, true);
                AppendVector(builder, 12, "scale", transform.localScale, true);
                AppendMoveParams(builder, 12, element);

                builder.Append("        }").Append(i < elements.Count - 1 ? "," : string.Empty).Append("\n");
            }

            builder.Append("    ]\n");
            builder.Append("}\n");

            return builder.ToString();
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

        private static void AppendVector(StringBuilder builder, int indent, string key, Vector3 value, bool trailingComma)
        {
            string pad = new string(' ', indent);
            string padInner = new string(' ', indent + 4);

            builder.Append(pad).Append('"').Append(key).Append("\": {\n");
            builder.Append(padInner).Append("\"x\": ").Append(Format(value.x)).Append(",\n");
            builder.Append(padInner).Append("\"y\": ").Append(Format(value.y)).Append(",\n");
            builder.Append(padInner).Append("\"z\": ").Append(Format(value.z)).Append("\n");
            builder.Append(pad).Append('}').Append(trailingComma ? ",\n" : "\n");
        }

        private static void AppendMoveParams(StringBuilder builder, int indent, VR_EditableElement element)
        {
            string pad = new string(' ', indent);

            if (!element.hasMovement || element.duration <= 0f)
            {
                builder.Append(pad).Append("\"moveParams\": null\n");
                return;
            }

            builder.Append(pad).Append("\"moveParams\": {\n");
            AppendVector(builder, indent + 4, "startPosition", element.WorldMoveStart, true);
            AppendVector(builder, indent + 4, "endPosition", element.WorldMoveEnd, true);
            builder.Append(new string(' ', indent + 4))
                   .Append("\"duration\": ").Append(Format(element.duration)).Append("\n");
            builder.Append(pad).Append("}\n");
        }

        /// <summary>Format invariant (point decimal) pour un JSON valide partout.</summary>
        private static string Format(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
        }
    }
}
