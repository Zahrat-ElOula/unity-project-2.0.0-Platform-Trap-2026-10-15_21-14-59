#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FactoryFrenzy.VR;
public class PrefabSetupTool
{
    [MenuItem("Factory Frenzy/Setup VR Prefabs")]
    static void SetupVRPrefabs()
    {
        string sourcePath = "Assets/FactooryFrenzy/Prefabs/Platform and Trap";
        string targetPath = "Assets/FactooryFrenzy/Prefabs_VR_Editor";

        // Crée le dossier cible s'il n'existe pas
        if (!AssetDatabase.IsValidFolder(targetPath))
            AssetDatabase.CreateFolder("Assets/FactooryFrenzy", "Prefabs_VR_Editor");

        // Mapping des prefabs vers leurs elementType
        var mapping = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Platform Start", "Platform_Start" },
            { "Platform End", "Platform_End" },
            { "Platform Classical 80", "Platform_Classical" },
            { "Platform Classical 360", "Platform_Classical" },
            { "Platform Classical 520", "Platform_Classical" },
            { "Platform Move 520", "Platform_Move" },
            { "Platform Trampoline", "Platform_Trampoline" },
            { "Platform Checkpoint", "Platform_Checkpoint" },
            { "Trap bumper", "Trap_Bumper" },
            { "Trap fan", "Trap_Fan" },
            { "Trap shooter", "Trap_Shooter" }
        };

        int count = 0;
        foreach (var kvp in mapping)
        {
            string sourceFile = $"{sourcePath}/{kvp.Key}.prefab";
            string targetFile = $"{targetPath}/{kvp.Key}.prefab";

            // Copie le prefab s'il n'existe pas déjà
            if (!System.IO.File.Exists(targetFile))
            {
                AssetDatabase.CopyAsset(sourceFile, targetFile);
            }

            // Charge la copie et ajoute le script
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetFile);
            if (prefab == null)
            {
                Debug.LogError($"Impossible de charger le prefab : {targetFile}");
                continue;
            }

            var editable = prefab.GetComponent<VR_EditableElement>();
            if (editable == null)
            {
                editable = prefab.AddComponent<VR_EditableElement>();
            }
            editable.elementType = kvp.Value;

            // Sauvegarde
            PrefabUtility.SavePrefabAsset(prefab);
            count++;
        }

        AssetDatabase.Refresh();
        Debug.Log($" {count} prefabs VR configurés avec succès !");
    }
}
#endif