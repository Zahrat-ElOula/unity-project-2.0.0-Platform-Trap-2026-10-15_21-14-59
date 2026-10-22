using FactoryFrenzy.PC;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-TRA-T2 (piège Lanceur).
    ///   - instancie le visuel "Trap shooter" dans un root "ShooterTrap" qui pivote,
    ///   - ajoute un point de tir "Muzzle" à l'avant,
    ///   - place le piège près du départ, orienté vers le parcours,
    ///   - câble le script ShooterTrap (joueur + tourelle + muzzle),
    ///   - sauvegarde la scène.
    /// </summary>
    public static class PcShooterTrapSetup
    {
        private const string ShooterPrefabPath =
            "Assets/FactoryFrenzy/Prefabs/Platform and Trap/Trap shooter.prefab";

        private const float VisualScale = 10f; // le prefab est minuscule -> agrandi pour être visible

        [MenuItem("Tools/Factory Frenzy/9) Setup PC-TRA-T2 (piège lanceur)", false, 9)]
        public static void SetupShooterTrap()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            GameObject player = FindPlayer();
            if (player == null)
            {
                Debug.LogError("[FF-Repair] Joueur (ThirdPersonController) introuvable. Lance d'abord le menu 2.");
                return;
            }

            GameObject existing = FindByName("ShooterTrap");
            if (existing != null)
            {
                Debug.Log("[FF-Repair] Un 'ShooterTrap' existe déjà, rien à créer.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShooterPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[FF-Repair] Prefab introuvable : " + ShooterPrefabPath);
                return;
            }

            // --- Root qui pivote (porte le script) --------------------------------
            GameObject root = new GameObject("ShooterTrap", typeof(ShooterTrap));
            Undo.RegisterCreatedObjectUndo(root, "Créer ShooterTrap");

            // --- Visuel (le prefab fourni), seulement agrandi, jamais modifié ----
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * VisualScale;

            // --- Point de tir (devant le canon) -----------------------------------
            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.4f, 0.8f);

            // --- Placement près du départ, orienté vers le parcours --------------
            GameObject startPlatform = FindByName("Platform Start");
            if (startPlatform != null)
            {
                Bounds startBounds = GetWorldBounds(startPlatform);
                Vector3 position = startBounds.center + new Vector3(-6f, 1.5f, 0f);
                root.transform.position = position;

                Vector3 look = startBounds.center - position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                {
                    root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                }
            }
            else
            {
                Debug.LogWarning("[FF-Repair] 'Platform Start' introuvable : piège placé à l'origine.");
            }

            // --- Câblage ----------------------------------------------------------
            ShooterTrap trap = root.GetComponent<ShooterTrap>();
            SerializedObject serialized = new SerializedObject(trap);
            serialized.FindProperty("player").objectReferenceValue = player.transform;
            serialized.FindProperty("turret").objectReferenceValue = root.transform;
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
            serialized.ApplyModifiedProperties();

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[FF-Repair] PC-TRA-T2 prêt : piège Lanceur placé près du départ. " +
                      "Play et approche-toi -> il t'oriente puis te tire dessus.");
        }

        // ---------------------------------------------------------------------
        // Utilitaires
        // ---------------------------------------------------------------------

        private static GameObject FindPlayer()
        {
            ThirdPersonController pc = Object.FindObjectOfType<ThirdPersonController>(true);
            return pc != null ? pc.gameObject : null;
        }

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
