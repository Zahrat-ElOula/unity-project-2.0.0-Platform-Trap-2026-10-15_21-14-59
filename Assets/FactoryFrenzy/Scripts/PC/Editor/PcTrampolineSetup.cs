using FactoryFrenzy.PC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-PLA-P6 (plateforme trampoline).
    ///   - instancie le visuel "Platform Trampoline" (redimensionné),
    ///   - ajoute un collider solide (support) + un volume trigger sur le dessus,
    ///   - place la plateforme derrière la plateforme de départ, même hauteur,
    ///   - sauvegarde la scène.
    /// </summary>
    public static class PcTrampolineSetup
    {
        private const string TrampolinePrefabPath =
            "Assets/FactoryFrenzy/Prefabs/Platform and Trap/Platform Trampoline.prefab";

        private const float TargetLength = 2.5f; // taille visuelle cible (m)

        [MenuItem("Tools/Factory Frenzy/11) Setup PC-PLA-P6 (trampoline)", false, 11)]
        public static void SetupTrampoline()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            if (FindByName("Trampoline") != null)
            {
                Debug.Log("[FF-Repair] Un 'Trampoline' existe déjà, rien à créer.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrampolinePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[FF-Repair] Prefab introuvable : " + TrampolinePrefabPath);
                return;
            }

            // --- Root qui porte le script et les colliders -----------------------
            GameObject root = new GameObject("Trampoline", typeof(Trampoline));
            Undo.RegisterCreatedObjectUndo(root, "Créer Trampoline");
            root.transform.position = Vector3.zero;

            // --- Visuel (prefab fourni, seulement redimensionné) -----------------
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Bounds baseBounds = GetWorldBounds(visual);
            float longest = Mathf.Max(baseBounds.size.x, baseBounds.size.z);
            float scale = longest > 0.001f ? TargetLength / longest : 8f;
            visual.transform.localScale = Vector3.one * scale;

            Bounds bounds = GetWorldBounds(visual);

            // --- Collider solide (support) ---------------------------------------
            BoxCollider solid = Undo.AddComponent<BoxCollider>(root);
            solid.center = root.transform.InverseTransformPoint(bounds.center);
            solid.size = bounds.size;

            // --- Volume trigger sur le dessus (déclenche le rebond) --------------
            const float bandHeight = 0.6f;
            float bottomY = bounds.max.y - 0.1f;
            Vector3 bandCenter = new Vector3(bounds.center.x, bottomY + bandHeight * 0.5f, bounds.center.z);

            BoxCollider trigger = Undo.AddComponent<BoxCollider>(root);
            trigger.isTrigger = true;
            trigger.center = root.transform.InverseTransformPoint(bandCenter);
            trigger.size = new Vector3(bounds.size.x * 0.9f, bandHeight, bounds.size.z * 0.9f);

            // --- Placement derrière la plateforme de départ -----------------------
            GameObject startPlatform = FindByName("Platform Start");
            if (startPlatform != null)
            {
                Bounds startBounds = GetWorldBounds(startPlatform);
                Vector3 position = new Vector3(
                    startBounds.center.x - bounds.center.x,
                    startBounds.max.y - bounds.max.y,
                    startBounds.min.z - 0.05f - bounds.max.z);
                root.transform.position = position;
            }
            else
            {
                Debug.LogWarning("[FF-Repair] 'Platform Start' introuvable : trampoline laissé à l'origine.");
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[FF-Repair] PC-PLA-P6 prêt : trampoline derrière le départ. " +
                      "Marche dessus -> tu rebondis vers le haut.");
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
