using FactoryFrenzy.PC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-TRA-T3 (piège Ventilateur).
    ///   - instancie le visuel "Trap fan" (redimensionné),
    ///   - crée un volume de poussée (BoxCollider trigger) orienté selon le transform,
    ///   - place le ventilateur près du parcours,
    ///   - sauvegarde la scène.
    /// (Les particules blanches sont créées à l'exécution par FanTrap.)
    /// </summary>
    public static class PcFanTrapSetup
    {
        private const string FanPrefabPath =
            "Assets/FactoryFrenzy/Prefabs/Platform and Trap/Trap fan.prefab";

        private const float TargetSize = 1.2f;

        [MenuItem("Tools/Factory Frenzy/14) Setup PC-TRA-T3 (piège ventilateur)", false, 14)]
        public static void SetupFanTrap()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            if (FindByName("FanTrap") != null)
            {
                Debug.Log("[FF-Repair] Un 'FanTrap' existe déjà, rien à créer.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FanPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[FF-Repair] Prefab introuvable : " + FanPrefabPath);
                return;
            }

            // --- Root qui porte le script et le volume ---------------------------
            GameObject root = new GameObject("FanTrap", typeof(FanTrap));
            Undo.RegisterCreatedObjectUndo(root, "Créer FanTrap");

            // --- Visuel (prefab fourni, seulement redimensionné) -----------------
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Bounds baseBounds = GetWorldBounds(visual);
            float longest = Mathf.Max(baseBounds.size.x, baseBounds.size.z);
            float scale = longest > 0.001f ? TargetSize / longest : 8f;
            visual.transform.localScale = Vector3.one * scale;

            // --- Volume d'air (trigger) orienté vers +Z du root ------------------
            const float airLength = 4f;
            const float airWidth = 2f;
            const float airHeight = 1f;
            BoxCollider airZone = Undo.AddComponent<BoxCollider>(root);
            airZone.isTrigger = true;
            airZone.size = new Vector3(airWidth, airHeight, airLength);
            airZone.center = new Vector3(0f, 0.2f, airLength * 0.5f);

            // --- Placement derrière le trampoline ---------------------------------
            GameObject refPlatform = FindByName("Trampoline") ?? FindByName("Platform Start");
            if (refPlatform != null)
            {
                Bounds refBounds = GetWorldBounds(refPlatform);
                root.transform.position = new Vector3(
                    refBounds.center.x,
                    refBounds.max.y,
                    refBounds.min.z - 1f);
                root.transform.rotation = Quaternion.identity;
            }
            else
            {
                root.transform.position = Vector3.zero;
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[FF-Repair] PC-TRA-T3 prêt : ventilateur placé. " +
                      "Entre dans son volume -> il te pousse dans la direction +Z du FanTrap, " +
                      "avec un souffle de particules blanches.");
        }

        // ---------------------------------------------------------------------
        // Utilitaires
        // ---------------------------------------------------------------------

        private static GameObject FindByName(string name)
        {
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go.name == name) return go;
            }
            return null;
        }

        private static Bounds GetWorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
