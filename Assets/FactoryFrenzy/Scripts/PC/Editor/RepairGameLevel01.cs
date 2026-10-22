using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur (ne s'exécute qu'à l'édition, jamais dans le build) qui répare la scène
    /// Game_Level_01 pour valider le jalon PC-PLA-P1 (Character Controller TPS) :
    ///   1. Il ne doit rester qu'UNE seule caméra de rendu, équipée d'un Cinemachine Brain.
    ///   2. Il ne doit rester qu'UN seul AudioListener, placé sur cette caméra.
    ///   3. La CinemachineVirtualCamera "PlayerFollowCamera" doit suivre PlayerCameraRoot
    ///      (Follow + Look At), sinon elle ne calcule rien.
    ///   4. Le joueur (PlayerArmature) doit être posé SUR la "Platform Start" et non à côté.
    /// Le tout via des références gérées par Unity (pas d'édition manuelle du fichier .unity).
    /// </summary>
    public static class RepairGameLevel01
    {
        // Chemins des prefabs fournis par le Starter Assets (on ne les modifie pas, on les instancie).
        private const string MainCameraPrefabPath   = "Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/MainCamera.prefab";
        private const string FollowCameraPrefabPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerFollowCamera.prefab";

        // ---------------------------------------------------------------------
        // MENU 1 : Diagnostic (ne modifie RIEN, se contente de logguer l'état)
        // ---------------------------------------------------------------------
        [MenuItem("Tools/Factory Frenzy/1) Diagnostiquer la scène", false, 1)]
        public static void Diagnose()
        {
            Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
            Debug.Log($"[FF-Diag] Caméras de rendu : {cameras.Length}");
            foreach (Camera cam in cameras)
            {
                bool hasBrain = cam.GetComponent<CinemachineBrain>() != null;
                bool hasListener = cam.GetComponent<AudioListener>() != null;
                Debug.Log($"[FF-Diag]  - '{cam.name}'  tag={cam.tag}  Brain={hasBrain}  AudioListener={hasListener}  actif={cam.gameObject.activeInHierarchy}");
            }

            AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>(true);
            Debug.Log($"[FF-Diag] AudioListeners : {listeners.Length}");

            CinemachineVirtualCamera[] vcams = Object.FindObjectsOfType<CinemachineVirtualCamera>(true);
            Debug.Log($"[FF-Diag] CinemachineVirtualCamera : {vcams.Length}");
            foreach (CinemachineVirtualCamera v in vcams)
            {
                Debug.Log($"[FF-Diag]  - '{v.name}'  Follow={(v.Follow != null ? v.Follow.name : "None")}  LookAt={(v.LookAt != null ? v.LookAt.name : "None")}");
            }

            GameObject player = FindByName("PlayerArmature");
            Debug.Log($"[FF-Diag] PlayerArmature : {(player != null ? player.transform.position.ToString() : "ABSENT")}");

            GameObject start = FindByName("Platform Start");
            Debug.Log($"[FF-Diag] Platform Start : {(start != null ? start.transform.position.ToString() : "ABSENT")}");
        }

        // ---------------------------------------------------------------------
        // MENU 2 : Réparation complète de la scène ouverte
        // ---------------------------------------------------------------------
        [MenuItem("Tools/Factory Frenzy/2) Réparer Game_Level_01 (PC-PLA-P1)", false, 2)]
        public static void Repair()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();

            // --- 1. Caméra de rendu unique, avec Cinemachine Brain ---
            List<Camera> cameras = Object.FindObjectsOfType<Camera>(true).ToList();
            Camera keep = cameras.FirstOrDefault(c => c.GetComponent<CinemachineBrain>() != null);

            if (keep == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainCameraPrefabPath);
                if (prefab != null)
                {
                    GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(go, "Ajout MainCamera Starter Assets");
                    keep = go.GetComponent<Camera>();
                    Debug.Log("[FF-Repair] MainCamera (Starter Assets) instanciée (avec Cinemachine Brain).");
                }
            }

            if (keep == null && cameras.Count > 0)
            {
                keep = cameras[0];
                Undo.AddComponent<CinemachineBrain>(keep.gameObject);
                Debug.LogWarning($"[FF-Repair] Aucun Cinemachine Brain trouvé : ajouté sur '{keep.name}'.");
            }

            if (keep == null)
            {
                Debug.LogError("[FF-Repair] Aucune caméra et prefab MainCamera introuvable. Réparation abandonnée.");
                return;
            }

            keep.gameObject.tag = "MainCamera";
            keep.enabled = true;
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam != keep)
                {
                    Undo.DestroyObjectImmediate(cam.gameObject);
                }
            }
            Debug.Log($"[FF-Repair] Caméra conservée : '{keep.name}' (les autres ont été supprimées).");

            // --- 2. Un seul AudioListener, sur la caméra conservée ---
            foreach (AudioListener listener in Object.FindObjectsOfType<AudioListener>(true).ToList())
            {
                if (listener.gameObject != keep.gameObject)
                {
                    Undo.DestroyObjectImmediate(listener);
                }
            }
            if (keep.GetComponent<AudioListener>() == null)
            {
                Undo.AddComponent<AudioListener>(keep.gameObject);
            }
            Debug.Log("[FF-Repair] AudioListener unique garanti sur la caméra.");

            // --- 3. Cinemachine virtual camera "PlayerFollowCamera" ---
            CinemachineVirtualCamera vcam = Object.FindObjectsOfType<CinemachineVirtualCamera>(true).FirstOrDefault();
            if (vcam == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefabPath);
                if (prefab != null)
                {
                    GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(go, "Ajout PlayerFollowCamera");
                    vcam = go.GetComponent<CinemachineVirtualCamera>();
                    Debug.Log("[FF-Repair] PlayerFollowCamera instanciée.");
                }
            }

            // --- 4. Câblage Follow / Look At vers PlayerCameraRoot ---
            GameObject player = FindByName("PlayerArmature");
            if (player != null && vcam != null)
            {
                vcam.gameObject.SetActive(true);
                Transform root = player.GetComponentsInChildren<Transform>(true)
                                       .FirstOrDefault(t => t.name == "PlayerCameraRoot");
                if (root == null)
                {
                    root = player.transform;
                    Debug.LogWarning("[FF-Repair] PlayerCameraRoot non trouvé, utilisation de la racine du joueur.");
                }

                Undo.RecordObject(vcam, "Câblage Follow/LookAt");
                vcam.Follow = root;
                vcam.LookAt = root;
                Debug.Log($"[FF-Repair] PlayerFollowCamera câblée sur '{root.name}' (Follow + Look At).");
            }
            else
            {
                Debug.LogWarning("[FF-Repair] PlayerArmature ou PlayerFollowCamera introuvable : câblage ignoré.");
            }

            // --- 5. Poser le joueur sur Platform Start (au-dessus de la surface réelle) ---
            GameObject start = FindByName("Platform Start");
            if (player != null && start != null)
            {
                if (TryGetBounds(start, out Bounds bounds))
                {
                    Undo.RecordObject(player.transform, "Placement joueur sur Platform Start");
                    player.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z);
                    Debug.Log($"[FF-Repair] Joueur placé en {player.transform.position} (au-dessus de Platform Start).");
                }
                else
                {
                    Debug.LogWarning("[FF-Repair] Platform Start sans collider/renderer : placement impossible.");
                }
            }
            else
            {
                Debug.LogWarning("[FF-Repair] PlayerArmature ou Platform Start introuvable : placement ignoré.");
            }

            // --- 6. Sauvegarde de la scène ---
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FF-Repair] Terminé et sauvegardé. Appuie sur Play pour tester PC-PLA-P1.");
        }

        // ---------------------------------------------------------------------
        // MENU 3 : Agrandir Platform Start à une taille jouable
        // ---------------------------------------------------------------------
        [MenuItem("Tools/Factory Frenzy/3) Agrandir Platform Start (~8 m)", false, 3)]
        public static void EnlargeStartPlatform()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            const float targetSize = 8f; // largeur/longueur visée en mètres

            GameObject start = FindByName("Platform Start");
            if (start == null)
            {
                Debug.LogError("[FF-Repair] Platform Start introuvable dans la scène.");
                return;
            }

            if (!TryGetBounds(start, out Bounds bounds))
            {
                Debug.LogError("[FF-Repair] Platform Start sans collider/renderer : impossible de mesurer.");
                return;
            }

            // Taille "de base" (échelle 1) = taille monde / échelle actuelle, puis on vise targetSize.
            float lossyX = Mathf.Max(0.0001f, Mathf.Abs(start.transform.lossyScale.x));
            float baseSizeX = bounds.size.x / lossyX;
            float uniformScale = targetSize / Mathf.Max(0.0001f, baseSizeX);

            Undo.RecordObject(start.transform, "Agrandir Platform Start");
            start.transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
            Debug.Log($"[FF-Repair] Platform Start : échelle {uniformScale:F1} (cible ~{targetSize} m).");

            // Re-placer le joueur sur la nouvelle surface.
            GameObject player = FindByName("PlayerArmature");
            if (player != null)
            {
                Physics.SyncTransforms(); // oblige Unity à recalculer les bounds tout de suite
                if (TryGetBounds(start, out Bounds resized))
                {
                    Undo.RecordObject(player.transform, "Replacement joueur sur Platform Start");
                    player.transform.position = new Vector3(resized.center.x, resized.max.y + 0.1f, resized.center.z);
                    Debug.Log($"[FF-Repair] Joueur replacé en {player.transform.position}.");
                }
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FF-Repair] Platform Start agrandie et scène sauvegardée. Teste en Play.");
        }

        // ---------------------------------------------------------------------
        // MENU 4 : Rendre le sol Forest marchable (ajoute des MeshCollider)
        // ---------------------------------------------------------------------
        [MenuItem("Tools/Factory Frenzy/4) Rendre le sol Forest marchable", false, 4)]
        public static void MakeForestGroundWalkable()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            // On ne rend solides QUE les surfaces de terrain (sol + blocs).
            // Les arbres/rochers/herbes/caisses ne reçoivent pas de collider ici.
            string[] solidPrefixes = { "ForestGround", "ForestBlock" };

            int added = 0;
            int alreadyCollidable = 0;

            foreach (MeshFilter mf in Object.FindObjectsOfType<MeshFilter>(true))
            {
                GameObject go = mf.gameObject;
                if (mf.sharedMesh == null)
                {
                    continue;
                }

                // Pas de doublon : si l'objet a déjà un collider, on ne fait rien.
                if (go.GetComponent<Collider>() != null)
                {
                    alreadyCollidable++;
                    continue;
                }

                string sourceName = SourcePrefabName(go);
                bool isGround = solidPrefixes.Any(prefix =>
                    go.name.StartsWith(prefix) || sourceName.StartsWith(prefix));
                if (!isGround)
                {
                    continue;
                }

                // MeshCollider : collider exact calqué sur la forme du maillage.
                MeshCollider collider = Undo.AddComponent<MeshCollider>(go);
                collider.sharedMesh = mf.sharedMesh;
                collider.convex = false; // surface statique -> non convexe (plus précis)
                added++;
            }

            Physics.SyncTransforms(); // recalcule la physique tout de suite

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FF-Repair] Sol Forest rendu marchable : {added} MeshCollider ajoutés ({alreadyCollidable} objets déjà collisionnés ignorés). Scène sauvegardée.");
        }

        // ---------------------------------------------------------------------
        // Utilitaires
        // ---------------------------------------------------------------------

        /// <summary>Cherche un GameObject par nom exact, actifs et inactifs confondus.</summary>
        private static GameObject FindByName(string name)
        {
            return Object.FindObjectsOfType<GameObject>(true)
                         .FirstOrDefault(go => go.name == name);
        }

        /// <summary>
        /// Nom du prefab source d'un objet de scène (utile car une instance s'appelle
        /// "ForestGround01 (12)" alors que le prefab source s'appelle "ForestGround01").
        /// </summary>
        private static string SourcePrefabName(GameObject go)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return source != null ? source.name : go.name;
        }

        /// <summary>
        /// Calcule la boîte englobante monde d'un objet en priorité via ses Colliders
        /// (ce qui donne la surface réelle sur laquelle le joueur peut se tenir).
        /// </summary>
        private static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
                return true;
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                return true;
            }

            bounds = default;
            return false;
        }
    }
}
