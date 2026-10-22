using FactoryFrenzy.PC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.EditorTools
{
    /// <summary>
    /// Outil éditeur pour le jalon PC-PLA-P5 (plateforme mouvante).
    ///   - instancie le visuel "Platform Move 520" (agrandi à ~3 m),
    ///   - ajoute un collider solide (support) + un trigger (détection du joueur),
    ///   - place la plateforme devant la plateforme de départ, sur la même hauteur,
    ///   - définit la destination (endPosition) et le temps de trajet,
    ///   - sauvegarde la scène.
    /// </summary>
    public static class PcMovingPlatformSetup
    {
        private const string MovePrefabPath =
            "Assets/FactoryFrenzy/Prefabs/Platform and Trap/Platform Move 520.prefab";

        private const float TargetLength = 3f;   // taille visuelle cible (m)
        private const float TravelDistance = 6f; // distance parcourue (m)
        private const float TravelTime = 4f;     // durée du trajet (s)

        // Position cible fixe (monde) où doit se trouver la plateforme.
        // Mettre à (float.NaN, ...) pour revenir au placement automatique sous le joueur.
        private static readonly Vector3 FixedTargetPosition = new Vector3(7.92f, float.NaN, -20.12f);

        [MenuItem("Tools/Factory Frenzy/10) Setup PC-PLA-P5 (plateforme mouvante)", false, 10)]
        public static void SetupMovingPlatform()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FF-Repair] À lancer en mode Édition (quitte le mode Play d'abord).");
                return;
            }

            GameObject existing = FindByName("MovingPlatform");
            if (existing != null)
            {
                // Réparation : s'assurer qu'il existe bien un collider SOLIDE
                // (le setup d'origine pouvait n'avoir laissé que le trigger).
                RepairSolidCollider(existing);
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MovePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[FF-Repair] Prefab introuvable : " + MovePrefabPath);
                return;
            }

            // --- Root qui porte le script et les colliders -----------------------
            GameObject root = new GameObject("MovingPlatform", typeof(MovingPlatform));
            Undo.RegisterCreatedObjectUndo(root, "Créer MovingPlatform");
            root.transform.position = Vector3.zero;

            // --- Visuel (prefab fourni, seulement agrandi) -----------------------
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Bounds baseBounds = GetWorldBounds(visual);
            float longest = Mathf.Max(baseBounds.size.x, baseBounds.size.z);
            float scale = longest > 0.001f ? TargetLength / longest : 12f;
            visual.transform.localScale = Vector3.one * scale;

            // --- Bornes du visuel une fois le root à l'origine (repère local = monde).
            Bounds bounds = GetWorldBounds(visual);

            // --- Taille de support : au moins 3 x 3 m (assez large pour le joueur) --
            const float minSupport = 3f;
            Vector3 solidSize = new Vector3(
                Mathf.Max(bounds.size.x, minSupport),
                Mathf.Max(bounds.size.y, 0.2f),
                Mathf.Max(bounds.size.z, minSupport));

            // --- Collider solide (le joueur peut se tenir dessus) ----------------
            BoxCollider solid = Undo.AddComponent<BoxCollider>(root);
            solid.center = root.transform.InverseTransformPoint(bounds.center);
            solid.size = solidSize;

            // --- Trigger (détecte le joueur qui monte dessus) --------------------
            const float triggerHeight = 3f;
            float bottomY = bounds.max.y - 0.2f;
            Vector3 triggerWorldCenter = new Vector3(bounds.center.x, bottomY + triggerHeight * 0.5f, bounds.center.z);

            BoxCollider trigger = Undo.AddComponent<BoxCollider>(root);
            trigger.isTrigger = true;
            trigger.center = root.transform.InverseTransformPoint(triggerWorldCenter);
            trigger.size = new Vector3(solidSize.x, triggerHeight, solidSize.z);

            // --- Placement : position fixe si définie, sinon sous le joueur ---------
            GameObject player = FindPlayer();
            GameObject startPlatform = FindByName("Platform Start");

            // X/Z cibles : la position fixe si fournie, sinon le joueur, sinon Platform Start.
            float targetX;
            float targetZ;
            bool haveTarget = false;

            if (!float.IsNaN(FixedTargetPosition.x) && !float.IsNaN(FixedTargetPosition.z))
            {
                targetX = FixedTargetPosition.x;
                targetZ = FixedTargetPosition.z;
                haveTarget = true;
            }
            else if (player != null)
            {
                targetX = player.transform.position.x;
                targetZ = player.transform.position.z;
                haveTarget = true;
            }
            else if (startPlatform != null)
            {
                Bounds sb = GetWorldBounds(startPlatform);
                targetX = sb.center.x;
                targetZ = sb.max.z;
                haveTarget = true;
            }
            else
            {
                targetX = 0f;
                targetZ = 0f;
            }

            if (haveTarget)
            {
                // Hauteur du sol à cet endroit (sinon Y fixe).
                float surfaceY = !float.IsNaN(FixedTargetPosition.y)
                    ? FixedTargetPosition.y
                    : GroundYBelow(new Vector3(targetX, 50f, targetZ));

                // Centre du visuel posé sur le sol, centré sur la cible.
                Vector3 position = new Vector3(
                    targetX - bounds.center.x,
                    surfaceY - bounds.max.y,
                    targetZ - bounds.center.z);
                root.transform.position = position;
            }
            else
            {
                Debug.LogWarning("[FF-Repair] Aucune cible (fixe/joueur/Platform Start) : plateforme laissée à l'origine.");
            }

            // --- Destination + temps de trajet -----------------------------------
            // Le trajet se fait le long de l'axe Z (devant le joueur).
            root.GetComponent<MovingPlatform>().Configure(
                root.transform.position + new Vector3(0f, 0f, TravelDistance), TravelTime);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[FF-Repair] PC-PLA-P5 prêt : plateforme mouvante devant le départ " +
                      $"(va-et-vient de {TravelDistance} m en {TravelTime} s). Monte dessus -> tu es transporté.");
        }

        // ---------------------------------------------------------------------
        // Réparation d'une plateforme existante
        // ---------------------------------------------------------------------

        /// <summary>
        /// S'assure que la plateforme possède un BoxCollider SOLIDE (non-trigger)
        /// qui couvre le visuel : sans lui le joueur ne peut pas se tenir dessus
        /// et la détection de transport (raycast) ne trouve aucun solide "own".
        /// </summary>
        private static void RepairSolidCollider(GameObject root)
        {
            // Le visuel = premier enfant (prefab "Platform Move 520").
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError("[FF-Repair] Aucun visuel sous 'MovingPlatform' : rien à réparer.");
                return;
            }

            // 1) Repositionne d'abord la plateforme (position fixe si définie).
            GameObject player = FindPlayer();
            GameObject startPlatform = FindByName("Platform Start");

            float surfaceY;
            float playerX;
            float playerZ;

            if (!float.IsNaN(FixedTargetPosition.x) && !float.IsNaN(FixedTargetPosition.z))
            {
                playerX = FixedTargetPosition.x;
                playerZ = FixedTargetPosition.z;
                surfaceY = !float.IsNaN(FixedTargetPosition.y)
                    ? FixedTargetPosition.y
                    : GroundYBelow(new Vector3(playerX, 50f, playerZ));
            }
            else if (player != null)
            {
                playerX = player.transform.position.x;
                playerZ = player.transform.position.z;
                surfaceY = GroundYBelow(player.transform.position);
            }
            else if (startPlatform != null)
            {
                Bounds sb = GetWorldBounds(startPlatform);
                playerX = sb.center.x;
                playerZ = sb.max.z;
                surfaceY = sb.max.y;
            }
            else
            {
                Debug.LogError("[FF-Repair] Ni position fixe, ni joueur, ni 'Platform Start' : impossible de placer la plateforme.");
                return;
            }

            // Décale le root de sorte que le visuel (bounds) soit centré sous le joueur.
            Bounds preBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) preBounds.Encapsulate(renderers[i].bounds);

            root.transform.position = new Vector3(
                root.transform.position.x + (playerX - preBounds.center.x),
                root.transform.position.y + (surfaceY - preBounds.max.y),
                root.transform.position.z + (playerZ - preBounds.center.z));

            // 2) Bornes après repositionnement → collider solide au bon endroit.
            Renderer[] moved = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = moved[0].bounds;
            for (int i = 1; i < moved.Length; i++) bounds.Encapsulate(moved[i].bounds);

            const float minSupport = 3f;
            Vector3 solidSize = new Vector3(
                Mathf.Max(bounds.size.x, minSupport),
                Mathf.Max(bounds.size.y, 0.2f),
                Mathf.Max(bounds.size.z, minSupport));

            // Corrige (ou crée) le BoxCollider solide.
            BoxCollider solid = null;
            foreach (BoxCollider box in root.GetComponents<BoxCollider>())
            {
                if (!box.isTrigger) { solid = box; break; }
            }
            if (solid == null)
            {
                solid = Undo.AddComponent<BoxCollider>(root);
                solid.isTrigger = false;
            }
            solid.center = root.transform.InverseTransformPoint(bounds.center);
            solid.size = solidSize;

            // Met à jour la destination (trajet le long de Z).
            root.GetComponent<MovingPlatform>().Configure(
                root.transform.position + new Vector3(0f, 0f, TravelDistance), TravelTime);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[FF-Repair] 'MovingPlatform' réparée : support {solidSize.x:F2} x {solidSize.z:F2} m, " +
                      $"surfacé à y={bounds.max.y:F2}, positionnée à {root.transform.position}. " +
                      $"Relance le Play : monte dessus -> tu es transporté.");
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

        private static GameObject FindByTag(string tag)
        {
            try { return GameObject.FindGameObjectWithTag(tag); }
            catch { return null; }
        }

        /// <summary>
        /// Trouve le joueur : d'abord par tag "Player", sinon par nom "PlayerArmature"
        /// (le prefab Starter Assets peut arriver sans tag dans la scène).
        /// </summary>
        private static GameObject FindPlayer()
        {
            GameObject byTag = FindByTag("Player");
            if (byTag != null) return byTag;
            return FindByName("PlayerArmature");
        }

        private static Bounds GetWorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Hauteur (Y monde) du sol juste sous le point donné (raycast vers le bas).</summary>
        private static float GroundYBelow(Vector3 from)
        {
            RaycastHit hit;
            if (Physics.Raycast(from + Vector3.up * 0.1f, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return from.y;
        }
    }
}
