using FactoryFrenzy.PC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-TRA-T1 (piège Bumper).
    ///   - instancie le visuel "Trap bumper" (redimensionné),
    ///   - ajoute un collider solide + un volume trigger sur le dessus,
    ///   - place le bumper entre la plateforme de départ et le parcours,
    ///   - sauvegarde la scène.
    /// </summary>
    public static class PcBumperTrapSetup
    {
        private const string BumperPrefabPath =
            "Assets/FactoryFrenzy/Prefabs/Platform and Trap/Trap bumper.prefab";

        private const float TargetSize = 1.8f; // diamètre/largeur visuelle

        [MenuItem("Tools/Factory Frenzy/13) Setup PC-TRA-T1 (piège bumper)", false, 13)]
        public static void SetupBumper()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            if (FindByName("BumperTrap") != null)
            {
                Debug.Log("[FF-Repair] Un 'BumperTrap' existe déjà, rien à créer.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BumperPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[FF-Repair] Prefab introuvable : " + BumperPrefabPath);
                return;
            }

            // --- Root qui porte le script et les colliders -----------------------
            GameObject root = new GameObject("BumperTrap", typeof(BumperTrap));
            Undo.RegisterCreatedObjectUndo(root, "Créer BumperTrap");

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

            Bounds bounds = GetWorldBounds(visual);

            // --- Collider solide (support) ---------------------------------------
            BoxCollider solid = Undo.AddComponent<BoxCollider>(root);
            solid.center = root.transform.InverseTransformPoint(bounds.center);
            solid.size = bounds.size;

            // --- Volume trigger englobant (déclenche le bump au contact) --------
            // On couvre TOUT le bumper + une marge vers le haut, sinon le joueur
            // grimpe dessus (le solide) sans jamais entrer dans une bande trop
            // étroite placée au-dessus.
            const float triggerMargin = 0.6f;
            BoxCollider trigger = Undo.AddComponent<BoxCollider>(root);
            trigger.isTrigger = true;
            trigger.center = root.transform.InverseTransformPoint(bounds.center);
            trigger.size = bounds.size + new Vector3(triggerMargin, triggerMargin * 2f, triggerMargin);

            // --- Placement entre départ et parcours -------------------------------
            GameObject startPlatform = FindByName("Platform Start");
            if (startPlatform != null)
            {
                Bounds startBounds = GetWorldBounds(startPlatform);
                Vector3 position = new Vector3(
                    startBounds.center.x + bounds.center.x,
                    startBounds.max.y - bounds.max.y,
                    startBounds.max.z + 0.4f - bounds.min.z);
                root.transform.position = position;
            }
            else
            {
                Debug.LogWarning("[FF-Repair] 'Platform Start' introuvable : bumper à l'origine.");
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[FF-Repair] PC-TRA-T1 prêt : bumper placé près du départ. " +
                      "Marche dessus -> tu es repoussé en direction opposée.");
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
