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
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEditor.Events;
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

        /// <summary>Dossier des prefabs sources fournis par le binome.</summary>
        public const string SourcePrefabsFolder = "Assets/FactoryFrenzy/Prefabs/Platform and Trap";

        /// <summary>Dossier des copies "RV" des prefabs (avec VR_EditableElement).</summary>
        public const string ElementPrefabsFolder = "Assets/FactoryFrenzy/Prefabs_VR_Editor";

        /// <summary>
        /// Table de correspondance : prefab source -> type logique exporte.
        /// Les 3 plateformes "Classical" partagent le meme type (Platform_Classical)
        /// mais des libelles d'interface differents.
        /// </summary>
        private static readonly ElementDefinition[] s_Elements =
        {
            new ElementDefinition("Platform Start", "Platform_Start", "Platform Start"),
            new ElementDefinition("Platform End", "Platform_End", "Platform End"),
            new ElementDefinition("Platform Checkpoint", "Platform_Checkpoint", "Checkpoint"),
            new ElementDefinition("Platform Classical 80", "Platform_Classical", "Classical 80"),
            new ElementDefinition("Platform Classical 360", "Platform_Classical", "Classical 360"),
            new ElementDefinition("Platform Classical 520", "Platform_Classical", "Classical 520"),
            new ElementDefinition("Platform Move 520", "Platform_Move", "Move 520 (mobile)",
                hasMovement: true, moveOffset: new Vector3(0f, 0f, 4f), moveDuration: 3f),
            new ElementDefinition("Platform Trampoline", "Platform_Trampoline", "Trampoline"),
            new ElementDefinition("Trap bumper", "Trap_Bumper", "Trap Bumper"),
            new ElementDefinition("Trap fan", "Trap_Fan", "Trap Fan"),
            new ElementDefinition("Trap shooter", "Trap_Shooter", "Trap Shooter")
        };

        /// <summary>Definition d'un element editable (prefab + type exporte).</summary>
        private sealed class ElementDefinition
        {
            public readonly string SourceName;
            public readonly string ElementType;
            public readonly string UiLabel;
            public readonly bool HasMovement;
            public readonly Vector3 MoveOffset;
            public readonly float MoveDuration;

            public ElementDefinition(string sourceName, string elementType, string uiLabel,
                bool hasMovement = false, Vector3 moveOffset = default, float moveDuration = 3f)
            {
                SourceName = sourceName;
                ElementType = elementType;
                UiLabel = uiLabel;
                HasMovement = hasMovement;
                MoveOffset = moveOffset;
                MoveDuration = moveDuration;
            }
        }

        // ---------------------------------------------------------------------
        // Menu Unity
        // ---------------------------------------------------------------------

        [MenuItem("Factory Frenzy/Editeur RV/1 - Construire la scene (XR + sol + decor)", false, 100)]
        public static void BuildStage1Menu()
        {
            BuildStage1();
        }

        [MenuItem("Factory Frenzy/Editeur RV/2 - Palette de selection et creation (VR-FP1-2)", false, 101)]
        public static void BuildStage2Menu()
        {
            BuildStage2();
        }

        [MenuItem("Factory Frenzy/Editeur RV/3 - Attraper et placer des objets (VR-FP1-3)", false, 102)]
        public static void BuildStage3Menu()
        {
            BuildStage3();
        }

        [MenuItem("Factory Frenzy/Editeur RV/4 - Supprimer des elements du niveau (VR-FP1-4)", false, 103)]
        public static void BuildStage4Menu()
        {
            BuildStage4();
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
            teleportationManager.teleportationProvider = UnityEngine.Object.FindObjectOfType<TeleportationProvider>();
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
        //  ETAPE 2  ->  VR-FP1-2 : selectionner et creer des elements dans une UI
        //  ---------------------------------------------------------------
        //  1. Duplication des 11 prefabs du binome vers
        //     Assets/FactoryFrenzy/Prefabs_VR_Editor/ : les originaux ne sont
        //     jamais modifies, chaque copie recoit un composant VR_EditableElement
        //     (type logique + parametres de mouvement de l'export JSON).
        //  2. Ajout du gestionnaire d'interface dans la scene :
        //      Managers
        //      +-- VRUIManager    (VR-FP1-2 : palette RV construite a l'execution)
        //
        //  La palette est generee au demarrage par VRUIManager a partir de la
        //  liste elementPrefabs : aucune intervention manuelle dans l'Inspector
        //  n'est necessaire.
        // =====================================================================

        public static void BuildStage2()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[VRSceneBuilder] La scene n'existe pas : executer d'abord l'etape 1.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 2.1 - Copies RV des prefabs d'elements.
            List<GameObject> elementPrefabs = EnsureElementPrefabs();
            if (elementPrefabs.Count == 0)
            {
                Debug.LogError("[VRSceneBuilder] Aucun prefab d'element copie : arret de l'etape 2.");
                return;
            }

            // 2.2 - Gestionnaire d'interface (palette RV).
            Transform managers = FindInScene("Managers");
            if (managers == null)
            {
                Debug.LogError("[VRSceneBuilder] Objet 'Managers' introuvable : executer d'abord l'etape 1.");
                return;
            }

            Transform existing = managers.Find("VRUIManager");
            GameObject uiGo = existing != null ? existing.gameObject : CreateEmpty("VRUIManager", Vector3.zero, managers).gameObject;

            VRUIManager uiManager = uiGo.GetComponent<VRUIManager>();
            if (uiManager == null) uiManager = uiGo.AddComponent<VRUIManager>();

            uiManager.elementPrefabs = elementPrefabs;
            uiManager.elementLabels = BuildLabels();

            Transform levelRoot = FindInScene("Level_Elements");
            if (levelRoot != null) uiManager.levelRoot = levelRoot;

            Transform spawnPoint = FindInScene("SpawnPoint");
            if (spawnPoint != null) uiManager.spawnPoint = spawnPoint;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VRSceneBuilder] Etape 2 terminee : {elementPrefabs.Count} prefabs d'elements dans " +
                      $"{ElementPrefabsFolder} et palette RV branchee dans la scene.");
        }

        // =====================================================================
        //  ETAPE 3  ->  VR-FP1-3 : attraper et placer des objets
        //  ---------------------------------------------------------------
        //  1. Chaque prefab de Prefabs_VR_Editor recoit les composants XR qui
        //     rendent l'element attrapable avec les manettes :
        //         Rigidbody          (isKinematic = true, useGravity = false)
        //         XRGrabInteractable (movementType = Kinematic,
        //                             throwOnDetach = false)
        //     Les colliders proviennent de la hierarchie du prefab ; si aucun
        //     collider n'est present, une boite englobante est ajoutee pour que
        //     l'element reste saisissable.
        //  2. Ajout du gestionnaire de manipulation dans la scene :
        //      Managers
        //      +-- VRGrabManager    (VR-FP1-3)
        // =====================================================================

        public static void BuildStage3()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[VRSceneBuilder] La scene n'existe pas : executer d'abord l'etape 1.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3.1 - Composants d'attrape sur les prefabs d'elements.
            int configured = EnsureGrabComponents();
            if (configured == 0)
            {
                Debug.LogError("[VRSceneBuilder] Aucun prefab d'element configure : executer d'abord l'etape 2.");
                return;
            }

            // 3.2 - Gestionnaire de manipulation.
            Transform managers = FindInScene("Managers");
            if (managers == null)
            {
                Debug.LogError("[VRSceneBuilder] Objet 'Managers' introuvable : executer d'abord l'etape 1.");
                return;
            }

            Transform existing = managers.Find("VRGrabManager");
            GameObject grabGo = existing != null ? existing.gameObject : CreateEmpty("VRGrabManager", Vector3.zero, managers).gameObject;

            VRGrabManager grabManager = grabGo.GetComponent<VRGrabManager>();
            if (grabManager == null) grabManager = grabGo.AddComponent<VRGrabManager>();

            grabManager.autoAddGrabComponents = true;
            grabManager.keepElementsFloating = true;
            grabManager.snapToGridOnRelease = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VRSceneBuilder] Etape 3 terminee : composants d'attrape sur {configured} prefabs " +
                      $"et VRGrabManager branche dans la scene.");
        }

        /// <summary>
        /// Ajoute / met a jour Rigidbody + XRGrabInteractable sur les prefabs
        /// d'elements de Prefabs_VR_Editor. Renvoie le nombre de prefabs traites.
        /// </summary>
        private static int EnsureGrabComponents()
        {
            int configured = 0;

            foreach (ElementDefinition definition in s_Elements)
            {
                string targetPath = ElementPrefabsFolder + "/" + definition.SourceName + ".prefab";
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
                if (asset == null)
                {
                    Debug.LogWarning("[VRSceneBuilder] Prefab d'element introuvable : " + targetPath);
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(targetPath);

                try
                {
                    // Rigidbody cinematique : l'element reste en place (pas de
                    // gravite) mais peut etre deplace par l'attrape XR.
                    Rigidbody body = root.GetComponent<Rigidbody>();
                    if (body == null) body = root.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;

                    // Securite : un interactable sans collider n'est pas saisissable.
                    if (root.GetComponentInChildren<Collider>(true) == null)
                    {
                        AddBoundsCollider(root);
                    }

                    XRGrabInteractable grab = root.GetComponent<XRGrabInteractable>();
                    if (grab == null) grab = root.AddComponent<XRGrabInteractable>();
                    grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                    grab.throwOnDetach = false;
                    grab.retainTransformParent = true;

                    PrefabUtility.SaveAsPrefabAsset(root, targetPath);
                    configured++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            return configured;
        }

        /// <summary>
        /// Ajoute un BoxCollider englobant les rendus d'un prefab sans collider.
        /// </summary>
        private static void AddBoundsCollider(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
            Debug.Log($"[VRSceneBuilder] BoxCollider englobant ajoute sur {root.name} " +
                      $"(aucun collider dans le prefab).");
        }

        // =====================================================================
        //  ETAPE 4  ->  VR-FP1-4 : supprimer des elements du niveau
        //  ---------------------------------------------------------------
        //  1. Ajout du gestionnaire de suppression :
        //      Managers
        //      +-- VRDeleteManager    (VR-FP1-4)
        //         - rayInteractor    : rayon d'INTERACTION de la manette droite
        //                              (le rayon de teleportation est ignore)
        //         - grabManager      : VRGrabManager (suppression interdite
        //                              pendant une prise)
        //  2. Relier le bouton "Supprimer" de la palette RV :
        //      VRUIManager.onDeleteRequested  ->  VRDeleteManager.DeleteTargeted
        // =====================================================================

        public static void BuildStage4()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[VRSceneBuilder] La scene n'existe pas : executer d'abord l'etape 1.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform managers = FindInScene("Managers");
            if (managers == null)
            {
                Debug.LogError("[VRSceneBuilder] Objet 'Managers' introuvable : executer d'abord l'etape 1.");
                return;
            }

            // 4.1 - Gestionnaire de suppression.
            Transform existing = managers.Find("VRDeleteManager");
            GameObject deleteGo = existing != null ? existing.gameObject : CreateEmpty("VRDeleteManager", Vector3.zero, managers).gameObject;

            VRDeleteManager deleteManager = deleteGo.GetComponent<VRDeleteManager>();
            if (deleteManager == null) deleteManager = deleteGo.AddComponent<VRDeleteManager>();

            // Rayon d'interaction de la main droite (jamais le rayon de teleportation).
            deleteManager.rayInteractor = FindRightHandInteractionRayInScene();
            deleteManager.grabManager = UnityEngine.Object.FindObjectOfType<VRGrabManager>();
            deleteManager.preventDeleteWhileGrabbing = true;
            deleteManager.createDefaultInputAction = true;

            if (deleteManager.rayInteractor == null)
            {
                Debug.LogWarning("[VRSceneBuilder] Rayon d'interaction droite introuvable dans la scene " +
                                 "(le VRDeleteManager le recherchera au lancement).");
            }

            // 4.2 - Relier le bouton "Supprimer" de la palette RV.
            VRUIManager uiManager = FindInSceneComponent<VRUIManager>();
            if (uiManager != null)
            {
                // Idempotent : on n'ajoute le lien que s'il n'existe pas deja,
                // l'etape reste reproductible si on la relance.
                if (uiManager.onDeleteRequested.GetPersistentEventCount() == 0)
                {
                    UnityEventTools.AddVoidPersistentListener(
                        uiManager.onDeleteRequested,
                        new UnityAction(deleteManager.DeleteTargeted));
                }
            }
            else
            {
                Debug.LogWarning("[VRSceneBuilder] VRUIManager introuvable : bouton 'Supprimer' non relie " +
                                 "(executer l'etape 2 d'abord).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VRSceneBuilder] Etape 4 terminee : VRDeleteManager cree, rayon " +
                      $"{(deleteManager.rayInteractor != null ? deleteManager.rayInteractor.name : "absent")} " +
                      $"et bouton 'Supprimer' relie a DeleteTargeted.");
        }

        /// <summary>
        /// Retourne le rayon d'interaction de la main droite : on ignore les
        /// rayons de teleportation (gabarit "Teleport Interactor") pour ne
        /// viser que les elements editables.
        /// </summary>
        private static XRRayInteractor FindRightHandInteractionRayInScene()
        {
            XRRayInteractor fallback = null;

            foreach (XRRayInteractor ray in Object.FindObjectsOfType<XRRayInteractor>())
            {
                string path = GetHierarchyPath(ray.transform).ToLowerInvariant();

                if (path.Contains("teleport")) continue; // rayon de teleportation
                if (path.Contains("right")) return ray;  // main droite
                if (fallback == null) fallback = ray;
            }

            return fallback;
        }

        /// <summary>Chemin hierarchique complet d'un transform (ex : "A/B/C").</summary>
        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;

            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        /// <summary>Recherche un composant de la scene actif par type.</summary>
        private static T FindInSceneComponent<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectOfType<T>();
        }

        /// <summary>
        /// Duplique les prefabs sources vers Prefabs_VR_Editor (si necessaire)
        /// puis configure leur composant VR_EditableElement.
        /// </summary>
        private static List<GameObject> EnsureElementPrefabs()
        {
            if (!Directory.Exists(ElementPrefabsFolder))
            {
                Directory.CreateDirectory(ElementPrefabsFolder);
                AssetDatabase.Refresh();
            }

            List<GameObject> result = new List<GameObject>();

            foreach (ElementDefinition definition in s_Elements)
            {
                string sourcePath = SourcePrefabsFolder + "/" + definition.SourceName + ".prefab";
                string targetPath = ElementPrefabsFolder + "/" + definition.SourceName + ".prefab";

                if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
                {
                    Debug.LogWarning("[VRSceneBuilder] Prefab source introuvable : " + sourcePath);
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) == null)
                {
                    if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                    {
                        Debug.LogWarning("[VRSceneBuilder] Copie impossible : " + sourcePath);
                        continue;
                    }

                    AssetDatabase.ImportAsset(targetPath);
                }

                ConfigureElementPrefab(targetPath, definition);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
                if (prefab != null) result.Add(prefab);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Ajoute / met a jour le composant VR_EditableElement d'un prefab copie.</summary>
        private static void ConfigureElementPrefab(string prefabPath, ElementDefinition definition)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                VR_EditableElement element = root.GetComponent<VR_EditableElement>();
                if (element == null) element = root.AddComponent<VR_EditableElement>();

                element.elementType = definition.ElementType;
                element.isLocked = false;
                element.hasMovement = definition.HasMovement;
                element.moveFrom = Vector3.zero;
                element.moveTo = definition.HasMovement ? definition.MoveOffset : Vector3.zero;
                element.duration = definition.MoveDuration;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string[] BuildLabels()
        {
            string[] labels = new string[s_Elements.Length];
            for (int i = 0; i < s_Elements.Length; i++) labels[i] = s_Elements[i].UiLabel;
            return labels;
        }

        /// <summary>Recherche un objet par nom dans la scene active.</summary>
        private static Transform FindInScene(string name)
        {
            Scene scene = SceneManager.GetActiveScene();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindRecursive(root.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
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
