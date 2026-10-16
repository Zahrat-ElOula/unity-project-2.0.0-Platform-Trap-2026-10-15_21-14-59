// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VRSceneBuilder.cs   (script d'EDITION, pas livre au runtime)
//  ---------------------------------------------------------------------------
//  Ce script construit la scene DemoScene_Forest de facon reproductible.
//  Il sert a deux choses :
//      1. generer / regenerer la scene et toute sa hierarchie sans travail
//         manuel dans l'Inspector ;
//      2. documenter la structure exacte de la scene (voir les commentaires de
//         chaque etape).
//
//  Utilisation dans l'editeur :
//      Menu  Factory Frenzy > Editeur RV > 1 - Construire la scene ...
//      (ou Unity -batchmode -quit -executeMethod
//             FactoryFrenzy.EditorTools.VRSceneBuilder.BuildStage1)
//
//  Chaque fonctionnalite VR-FP1-x possede sa fonction BuildStageX : il suffit
//  d'appeler BuildAllStages() (menu "Construire toutes les etapes") pour
//  reconstruire la scene complete depuis zero.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using FactoryFrenzy.VR;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Generateur de la scene DemoScene_Forest (editeur RV Factory Frenzy).
    /// </summary>
    public static class VRSceneBuilder
    {
        // ---------------------------------------------------------------------
        // Chemins
        // ---------------------------------------------------------------------

        public const string ScenesFolder = "Assets/FactoryFrenzy/Scenes";
        public const string ScenePath = ScenesFolder + "/DemoScene_Forest.unity";
        public const string MaterialsFolder = "Assets/FactoryFrenzy/Materials";
        public const string GroundMaterialPath = MaterialsFolder + "/Ground_Teleportation.mat";
        public const string XrSetupPrefabPath =
            "Assets/XR Interaction Toolkit/Starter Assets/Prefabs/XR Interaction Setup.prefab";
        public const string DecorFolder = "Assets/AurynSky/Forest Pack/Prefabs";

        // ---------------------------------------------------------------------
        // Menu Unity
        // ---------------------------------------------------------------------

        [MenuItem("Factory Frenzy/Editeur RV/1 - Construire la scene (XR + sol + decor)", false, 100)]
        public static void BuildStage1Menu()
        {
            BuildStage1();
        }

        [MenuItem("Factory Frenzy/Editeur RV/Ouvrir la scene DemoScene_Forest", false, 1)]
        public static void OpenSceneMenu()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[VRSceneBuilder] La scene n'existe pas encore : " + ScenePath);
            }
        }

        // =====================================================================
        //  ETAPE 1  ->  VR-FP1-1 : teleportation de l'avatar RV
        //  ---------------------------------------------------------------
        //  Hierarchie produite :
        //      DemoScene_Forest
        //      +-- Directional Light
        //      +-- XR Interaction Setup            (XR Origin + manettes +
        //      |                                    locomotion + teleportation
        //      |                                    + EventSystem + Interaction
        //      |                                    Manager, prefab XRI)
        //      +-- TeleportationGround             (sol, TeleportationArea)
        //      +-- Environment                     (decor forestier)
        //      +-- Level_Elements                  (racine des elements du niveau)
        //      +-- SpawnPoint                      (point de depart / de creation)
        //      +-- Managers
        //          +-- VRTeleportationManager      (VR-FP1-1)
        // =====================================================================

        public static void BuildStage1()
        {
            // 1.1 - Nouvelle scene vide.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!Directory.Exists(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
            }

            // 1.2 - Eclairage.
            CreateDirectionalLight();

            // 1.3 - Rig XR complet (XR Origin, manettes, locomotion, teleportation,
            //       EventSystem, XR Interaction Manager, Input Action Manager).
            GameObject xrSetup = InstantiateXrSetup();
            if (xrSetup == null) return; // erreur deja loggee

            // 1.4 - Sol teleportable : une TeleportationArea porte la teleportation.
            TeleportationArea teleportationArea = CreateTeleportationGround();

            // 1.5 - Decor forestier (prefabs sans collider : purement visuel).
            Transform environment = CreateEnvironment();

            // 1.6 - Points d'ancrage du niveau.
            Transform spawnPoint = CreateEmpty("SpawnPoint", new Vector3(0f, 1f, 1.5f), null);
            Transform levelRoot = CreateEmpty("Level_Elements", Vector3.zero, null);

            // 1.7 - Gestionnaires (racine "Managers").
            GameObject managersRoot = new GameObject("Managers");

            GameObject teleportationGo = CreateEmpty("VRTeleportationManager", Vector3.zero, managersRoot.transform).gameObject;
            VRTeleportationManager teleportationManager = teleportationGo.AddComponent<VRTeleportationManager>();
            teleportationManager.teleportationProvider = Object.FindObjectOfType<TeleportationProvider>();
            teleportationManager.teleportationArea = teleportationArea;
            teleportationManager.spawnPoint = spawnPoint;

            if (teleportationManager.teleportationProvider == null)
            {
                Debug.LogWarning("[VRSceneBuilder] TeleportationProvider introuvable dans le prefab XR Interaction Setup.");
            }

            // 1.8 - Sauvegarde + Build Settings.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VRSceneBuilder] Etape 1 terminee. Scene : {ScenePath}\n" +
                      $"Environnement : {environment.childCount} objets de decor, " +
                      $"sol teleportable : {(teleportationArea != null ? "OK" : "absent")}.");
        }

        // =====================================================================
        //  Outils internes
        // =====================================================================

        private static void CreateDirectionalLight()
        {
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.9f, 1f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static GameObject InstantiateXrSetup()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrSetupPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[VRSceneBuilder] Prefab introuvable : " + XrSetupPrefabPath +
                               "\n=> Importer l'exemple 'Starter Assets' du paquet XR Interaction Toolkit " +
                               "(Package Manager > XR Interaction Toolkit > Samples > Starter Assets > Import).");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "XR Interaction Setup";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            return instance;
        }

        private static TeleportationArea CreateTeleportationGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TeleportationGround";
            ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ground.transform.localScale = new Vector3(1.5f, 1f, 1.5f); // 15 m x 15 m

            // Materiau lisible (et different du prefab de sol du decor).
            Material material = GetOrCreateGroundMaterial();
            if (material != null)
            {
                ground.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            TeleportationArea area = ground.AddComponent<TeleportationArea>();
            area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;

            return area;
        }

        private static Material GetOrCreateGroundMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (existing != null) return existing;

            if (!Directory.Exists(MaterialsFolder))
            {
                Directory.CreateDirectory(MaterialsFolder);
                AssetDatabase.Refresh();
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material material = new Material(shader);
            material.name = "Ground_Teleportation";
            material.color = new Color(0.42f, 0.48f, 0.34f, 1f);

            AssetDatabase.CreateAsset(material, GroundMaterialPath);
            return material;
        }

        /// <summary>
        /// Decor forestier : uniquement des prefabs visuels (sans collider),
        /// le sol teleportable reste la seule surface de deplacement.
        /// </summary>
        private static Transform CreateEnvironment()
        {
            Transform root = CreateEmpty("Environment", Vector3.zero, null);

            AddDecor(root, "ForestTreePineTall", new Vector3(-5.5f, 0f, 4.5f), 15f);
            AddDecor(root, "ForestTreePineTall2", new Vector3(5.5f, 0f, 5.5f), -35f);
            AddDecor(root, "ForestTreePineShort", new Vector3(-6f, 0f, -3f), 80f);
            AddDecor(root, "ForestTreeAppleTall", new Vector3(6f, 0f, -4.5f), 200f);
            AddDecor(root, "ForestTreeAppleShort", new Vector3(-4.5f, 0f, -6f), 120f);
            AddDecor(root, "ForestBarrel", new Vector3(2.4f, 0f, 2.6f), 0f);
            AddDecor(root, "ForestCrate", new Vector3(3.2f, 0f, 1.9f), 25f);
            AddDecor(root, "ForestBlock01", new Vector3(-2.6f, 0f, -2f), 0f);
            AddDecor(root, "ForestBridgeRegular", new Vector3(0f, 0f, -6.5f), 0f);
            AddDecor(root, "ForestCastleWindow", new Vector3(-7f, 0f, 0.5f), 90f);
            AddDecor(root, "ForestGrass01", new Vector3(1.5f, 0f, -1.5f), 0f);
            AddDecor(root, "ForestGrass02", new Vector3(-1.2f, 0f, 1.8f), 45f);

            return root;
        }

        private static void AddDecor(Transform parent, string prefabName, Vector3 position, float yRotation, float scale = 1f)
        {
            string path = DecorFolder + "/" + prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogWarning("[VRSceneBuilder] Decor introuvable : " + path);
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = prefabName;
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yRotation, 0f));
            instance.transform.localScale = Vector3.one * scale;
        }

        private static Transform CreateEmpty(string name, Vector3 position, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            return go.transform;
        }

        private static void RegisterSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
