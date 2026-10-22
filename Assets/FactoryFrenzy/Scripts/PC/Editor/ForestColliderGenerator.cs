#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour rendre SOLIDES les props du pack Forest
    /// (tonneaux, caisses, arbres, châteaux, tours, ponts, boulets de canon,
    ///  braseros, échelles...).
    ///
    /// Objectifs :
    ///   1. Le joueur ne doit plus TRAVERSER ces objets.
    ///   2. Ces objets servent de BOUCLIER contre le projectile du piège "Shooter"
    ///      (le projectile est détruit au contact d'un collider solide).
    ///
    /// Principe :
    ///   - On ajoute des colliders PRIMITIFS (Box / Sphere / Capsule), jamais de MeshCollider
    ///     (bien trop coûteux pour des centaines d'objets).
    ///   - La taille/le centre sont calculés automatiquement à partir des Renderer
    ///     (y compris les enfants : une seule primitive englobe tout le prop).
    ///   - On travaille sur les INSTANCES de la scène ouverte : les prefabs source
    ///     ne sont PAS modifiés (ce sont des "overrides" d'instance).
    ///   - Aucun collider n'est ajouté si l'objet en possède déjà un (ré-exécutable).
    ///
    /// Ce script vit dans un dossier "Editor" et est entouré de #if UNITY_EDITOR :
    /// il n'est JAMAIS compilé dans le build final.
    /// </summary>
    public class ForestColliderGenerator : EditorWindow
    {
        // Fragments de nom considérés comme des OBSTACLES solides (casse ignorée).
        private static readonly string[] SolidFragments =
        {
            "Barrel", "Crate", "Bridge", "CannonBall", "Brazzier", "Brazier",
            "Tree", "Castle", "Tower", "Catapult", "Chest", "Ladder", "Block"
        };

        // Fragments à NE JAMAIS traiter (terrain, déco, eau, collectibles).
        private static readonly string[] ExcludeFragments =
        {
            "Ground", "Grass", "Water", "River", "Smoke", "Fire", "Pixie", "Coin"
        };

        // --- Réglages exposés dans la fenêtre ----------------------------------
        private float capsuleRadiusFactor = 1f;   // facteur de rayon pour capsules (arbres/tonneaux)
        private float globalShrink = 0.98f;       // marge pour ne pas « boursoufler » le collider
        private bool includeBlocks = false;       // ForestBlock : déjà gérés par la réparation du sol
        private Vector2 scroll;

        [MenuItem("Tools/Factory Frenzy/15) Colliders Forest (props)", false, 15)]
        public static void OpenWindow()
        {
            ForestColliderGenerator window = GetWindow<ForestColliderGenerator>(false, "Colliders Forest", true);
            window.minSize = new Vector2(380f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Rendre les props Forest solides", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Ajoute des colliders (Box / Sphere / Capsule) sur les props Forest de la scène ouverte.\n" +
                "Résultat : le joueur ne les traverse plus et ils bloquent le projectile du Shooter.\n" +
                "Les prefabs source ne sont PAS modifiés (overrides sur les instances).",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Réglages", EditorStyles.boldLabel);
            capsuleRadiusFactor = EditorGUILayout.Slider("Rayon capsule (arbres/tonneaux)", capsuleRadiusFactor, 0.1f, 1.5f);
            globalShrink = EditorGUILayout.Slider("Marge (shrink)", globalShrink, 0.5f, 1f);
            includeBlocks = EditorGUILayout.Toggle("Inclure les ForestBlock", includeBlocks);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Action principale", EditorStyles.boldLabel);
            if (GUILayout.Button("Générer automatiquement (toute la scène)", GUILayout.Height(30f)))
            {
                GenerateForScene();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Forçage manuel sur la sélection", EditorStyles.boldLabel);
            if (GUILayout.Button("Ajouter Box Collider (sélection)")) AddToSelection(typeof(BoxCollider));
            if (GUILayout.Button("Ajouter Capsule Collider (sélection)")) AddToSelection(typeof(CapsuleCollider));
            if (GUILayout.Button("Ajouter Sphere Collider (sélection)")) AddToSelection(typeof(SphereCollider));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Diagnostic", EditorStyles.boldLabel);
            if (GUILayout.Button("Compter les props sans collider")) Diagnose();

            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------------
        // Génération automatique sur toute la scène
        // ---------------------------------------------------------------------
        private void GenerateForScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Forest] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            int added = 0;
            int already = 0;
            int failed = 0;

            foreach (GameObject go in CollectProps(includeBlocks))
            {
                if (HasAnyCollider(go)) { already++; continue; }

                Type type = DetectColliderType(SourceName(go));
                if (AddCollider(go, type, capsuleRadiusFactor, globalShrink)) added++;
                else failed++;
            }

            Physics.SyncTransforms();
            SaveScene();

            Debug.Log($"[FF-Forest] Terminé : {added} collider(s) ajoutés, {already} déjà collisionnés, " +
                      $"{failed} ignorés (pas de Renderer). Scène sauvegardée.");
        }

        // ---------------------------------------------------------------------
        // Forçage manuel : ajoute un type précis aux objets sélectionnés
        // ---------------------------------------------------------------------
        private static void AddToSelection(Type type)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Forest] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[FF-Forest] Aucun objet sélectionné dans la Hiérarchie.");
                return;
            }

            int added = 0, already = 0, failed = 0;
            foreach (GameObject go in selection)
            {
                if (HasAnyCollider(go)) { already++; continue; }
                if (AddCollider(go, type, 1f, 0.98f)) added++; else failed++;
            }

            Physics.SyncTransforms();
            SaveScene();
            Debug.Log($"[FF-Forest] Sélection : {added} collider(s) ajoutés, {already} déjà collisionnés, {failed} ignorés.");
        }

        private void Diagnose()
        {
            int total = 0, missing = 0;
            foreach (GameObject go in CollectProps(includeBlocks))
            {
                total++;
                if (!HasAnyCollider(go)) missing++;
            }
            Debug.Log($"[FF-Forest] Props détectés dans la scène : {total} — sans collider : {missing}.");
        }

        // ---------------------------------------------------------------------
        // Collecte des props (instances racines de prefab dont le nom matche)
        // ---------------------------------------------------------------------
        private static List<GameObject> CollectProps(bool includeBlocks)
        {
            List<GameObject> result = new List<GameObject>();

            foreach (GameObject go in FindObjectsOfType<GameObject>(true))
            {
                // On ne traite que la racine d'une instance de prefab
                // (une seule primitive englobera tout le prop).
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if (root == null || root != go) continue;

                string sourceName = SourceName(go);
                if (!IsSolid(sourceName, includeBlocks)) continue;

                result.Add(go);
            }

            return result;
        }

        private static bool IsSolid(string name, bool includeBlocks)
        {
            if (string.IsNullOrEmpty(name)) return false;

            foreach (string exclude in ExcludeFragments)
            {
                if (name.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }

            foreach (string solid in SolidFragments)
            {
                if (solid == "Block" && !includeBlocks) continue;
                if (name.IndexOf(solid, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        /// <summary>
        /// Détection du type de collider selon le nom :
        ///   - boulet de canon        -> Sphere
        ///   - arbre / tonneau / brasero -> Capsule
        ///   - tout le reste          -> Box
        /// </summary>
        private static Type DetectColliderType(string name)
        {
            string n = (name ?? string.Empty).ToLowerInvariant();

            if (n.Contains("cannonball") || n.Contains("cannon")) return typeof(SphereCollider);
            if (n.Contains("tree") || n.Contains("barrel") || n.Contains("brazzier") || n.Contains("brazier"))
                return typeof(CapsuleCollider);

            return typeof(BoxCollider);
        }

        // ---------------------------------------------------------------------
        // Ajout d'un collider primitif dimensionné sur les Renderer
        // ---------------------------------------------------------------------
        private static bool AddCollider(GameObject root, Type type, float capsuleRadiusFactor, float shrink)
        {
            if (!TryGetLocalBounds(root, out Bounds localBounds)) return false;

            Vector3 size = new Vector3(
                Mathf.Abs(localBounds.size.x),
                Mathf.Abs(localBounds.size.y),
                Mathf.Abs(localBounds.size.z)) * shrink;
            Vector3 center = localBounds.center;

            if (type == typeof(SphereCollider))
            {
                SphereCollider sphere = Undo.AddComponent<SphereCollider>(root);
                sphere.center = center;
                sphere.radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;
                return true;
            }

            if (type == typeof(CapsuleCollider))
            {
                CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(root);
                capsule.direction = 1; // axe Y (vertical)
                capsule.center = center;

                float radius = Mathf.Min(size.x, size.z) * 0.5f * capsuleRadiusFactor;
                float height = size.y;
                if (height < radius * 2f) height = radius * 2f;

                capsule.radius = radius;
                capsule.height = height;
                return true;
            }

            // Par défaut : Box
            BoxCollider box = Undo.AddComponent<BoxCollider>(root);
            box.center = center;
            box.size = size;
            return true;
        }

        /// <summary>
        /// Calcule la boîte englobante des Renderer d'un objet, exprimée dans l'espace
        /// LOCAL de sa racine (indépendante de sa rotation/échelle dans la scène).
        /// </summary>
        private static bool TryGetLocalBounds(GameObject root, out Bounds localBounds)
        {
            localBounds = default;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;
            bool started = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;

                Bounds lb = renderer.localBounds;
                Matrix4x4 toRoot = worldToRoot * renderer.transform.localToWorldMatrix;
                Vector3 c = lb.center;
                Vector3 e = lb.extents;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);

                    Vector3 p = toRoot.MultiplyPoint3x4(corner);
                    if (!started)
                    {
                        localBounds = new Bounds(p, Vector3.zero);
                        started = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(p);
                    }
                }
            }

            return started;
        }

        // ---------------------------------------------------------------------
        // Utilitaires
        // ---------------------------------------------------------------------
        private static bool HasAnyCollider(GameObject go)
        {
            return go.GetComponentsInChildren<Collider>(true).Length > 0;
        }

        /// <summary>Nom du prefab source d'un objet (une instance s'appelle "ForestBarrel (2)").</summary>
        private static string SourceName(GameObject go)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return source != null ? source.name : go.name;
        }

        private static void SaveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
