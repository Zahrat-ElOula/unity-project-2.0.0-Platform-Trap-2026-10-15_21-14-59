using FactoryFrenzy.PC;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-PLA-P4 :
    ///   - place une "Platform Classical 80" à côté du départ (parcours testable),
    ///   - crée un "RespawnPoint" au-dessus du départ,
    ///   - crée un "RespawnManager" et le câble (joueur + point + seuil de chute),
    ///   - sauvegarde la scène.
    /// </summary>
    public static class PcRespawnSetup
    {
        private const string ClassicPrefabPath =
            "Assets/FactoryFrenzy/Prefabs/Platform and Trap/Platform Classical 80.prefab";

        // Distance de chute (m) sous le départ avant de réapparaître.
        private const float FallDepth = 8f;

        // Marge sous le niveau pour la "kill zone" (trigger de chute).
        private const float KillZoneDepth = 3f;
        private const float KillZoneThickness = 2f;

        [MenuItem("Tools/Factory Frenzy/8) Setup PC-PLA-P4 (plateforme classique + respawn)", false, 8)]
        public static void SetupRespawn()
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

            // Référence de départ : 'Platform Start' si présente, sinon le joueur.
            GameObject startPlatform = FindByName("Platform Start");
            Vector3 startCenter;
            float startSurfaceY;

            if (startPlatform != null)
            {
                Bounds sb = GetWorldBounds(startPlatform);
                startCenter = new Vector3(sb.center.x, sb.center.y, sb.center.z);
                startSurfaceY = sb.max.y;
            }
            else
            {
                // Le joueur est posé sur le sol : c'est notre point de départ.
                startCenter = player.transform.position;
                startSurfaceY = player.transform.position.y;
                Debug.Log("[FF-Repair] 'Platform Start' absente : départ basé sur le joueur.");
            }

            // --- 1) Plateforme classique (parcours) ------------------------------
            GameObject classic = FindByName("Platform Classical 80");
            if (classic == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClassicPrefabPath);
                if (prefab != null)
                {
                    classic = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    classic.name = "Platform Classical 80";

                    // Placement : à côté du départ (X décalé) si possible.
                    Vector3 targetCenter = startCenter;
                    Bounds targetBounds = GetWorldBounds(classic);
                    if (startPlatform != null)
                    {
                        PlaceBeside(startPlatform, classic, -1);
                    }
                    else
                    {
                        classic.transform.position = new Vector3(
                            targetCenter.x - targetBounds.center.x + (targetBounds.extents.x + 2f),
                            targetCenter.y,
                            targetCenter.z - targetBounds.center.z);
                    }
                    Debug.Log("[FF-Repair] 'Platform Classical 80' placée à côté du départ.");
                }
            }

            // --- 2) Point de réapparition (SUR le sol du départ) ------------------
            Transform respawnPoint = FindTransformByName("RespawnPoint");
            if (respawnPoint == null)
            {
                GameObject respawnGo = new GameObject("RespawnPoint");
                Undo.RegisterCreatedObjectUndo(respawnGo, "Créer RespawnPoint");
                respawnPoint = respawnGo.transform;
                Debug.Log("[FF-Repair] 'RespawnPoint' créé.");
            }

            // Place le point légèrement au-dessus de la surface de départ (pas dans le vide).
            respawnPoint.position = new Vector3(startCenter.x, startSurfaceY + 0.1f, startCenter.z);

            // --- 3) RespawnManager + câblage --------------------------------------
            RespawnManager manager = Object.FindObjectOfType<RespawnManager>(true);
            if (manager == null)
            {
                GameObject managerGo = new GameObject("RespawnManager", typeof(RespawnManager));
                Undo.RegisterCreatedObjectUndo(managerGo, "Créer RespawnManager");
                manager = managerGo.GetComponent<RespawnManager>();
            }

            float threshold = startSurfaceY - FallDepth;
            SerializedObject serialized = new SerializedObject(manager);
            serialized.FindProperty("player").objectReferenceValue = player.transform;
            serialized.FindProperty("respawnPoint").objectReferenceValue = respawnPoint;
            serialized.FindProperty("fallThreshold").floatValue = threshold;
            serialized.FindProperty("useKillTrigger").boolValue = true;
            serialized.ApplyModifiedProperties();

            // --- 4) Kill zone : grand trigger sous le niveau ----------------------
            // (méthode recommandée : OnTriggerEnter quand le joueur tombe dedans)
            Bounds levelBounds = GetLevelBounds(startPlatform, classic);
            float killY = levelBounds.min.y - KillZoneDepth;

            BoxCollider kill = manager.GetComponent<BoxCollider>();
            if (kill == null) kill = Undo.AddComponent<BoxCollider>(manager.gameObject);

            kill.isTrigger = true;
            // Le collider est porté par le GameObject du manager : on le positionne
            // sur la kill zone via le Transform.
            manager.transform.position = new Vector3(
                levelBounds.center.x, killY, levelBounds.center.z);

            kill.size = new Vector3(
                levelBounds.size.x + 20f,
                KillZoneThickness,
                levelBounds.size.z + 20f);
            kill.center = Vector3.zero;

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[FF-Repair] PC-PLA-P4 prêt. Kill zone sous le niveau à Y {killY:0.00} " +
                      $"(tombe dedans -> retour au départ). Seuil de secours = Y {threshold:0.00}.");
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

        private static Transform FindTransformByName(string name)
        {
            GameObject go = FindByName(name);
            return go != null ? go.transform : null;
        }

        private static Bounds GetWorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Bornes englobant la forêt et les plateformes de départ/classique.</summary>
        private static Bounds GetLevelBounds(GameObject startPlatform, GameObject classic)
        {
            bool has = false;
            Bounds bounds = new Bounds();

            if (startPlatform != null)
            {
                bounds = GetWorldBounds(startPlatform);
                has = true;
            }
            if (classic != null)
            {
                Bounds b = GetWorldBounds(classic);
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }

            // Ajoute les gros objets de terrain "Forest" s'ils existent.
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go.name.StartsWith("Forest") && go.GetComponentInChildren<Renderer>() != null)
                {
                    Bounds b = GetWorldBounds(go);
                    if (!has) { bounds = b; has = true; }
                    else bounds.Encapsulate(b);
                }
            }

            if (!has)
            {
                // Dernier recours : une boîte raisonnable autour de l'origine.
                bounds = new Bounds(Vector3.zero, new Vector3(100f, 40f, 100f));
            }
            return bounds;
        }

        /// <summary>Place 'target' à côté de 'reference' (side = -1 gauche, +1 droite), même hauteur.</summary>
        private static void PlaceBeside(GameObject reference, GameObject target, int side)
        {
            Bounds refBounds = GetWorldBounds(reference);
            Bounds targetBounds = GetWorldBounds(target);

            float offset = refBounds.extents.x + targetBounds.extents.x;
            Vector3 position = new Vector3(
                refBounds.center.x + side * offset,
                refBounds.center.y,
                refBounds.center.z);

            target.transform.position = position;
        }
    }
}
