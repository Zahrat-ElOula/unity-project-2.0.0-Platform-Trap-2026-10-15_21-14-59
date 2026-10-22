using System;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Plateforme mouvante (jalon PC-PLA-P5).
    /// Elle fait un aller-retour entre sa position de départ et 'endPosition'
    /// en 'travelTime' secondes (== champs endPosition/travelTime du contrat JSON).
    ///
    /// Point important : un CharacterController n'est PAS embarqué automatiquement
    /// par un collider qui bouge. On détecte donc le joueur via un volume trigger
    /// et on le déplace du même delta que la plateforme (LateUpdate).
    ///
    /// Depuis la contrainte "obstacles Forest" : avant chaque déplacement, on sonde
    /// la position suivante avec un BoxCast (== la boîte de la plateforme). Si un
    /// obstacle Forest est devant, la plateforme REBROUSSE chemin au lieu de le
    /// traverser. (Un Rigidbody kinematic aurait, lui, traversé les colliders.)
    /// </summary>
    public class MovingPlatform : MonoBehaviour
    {
        [Header("Déplacement")]
        [Tooltip("Position d'arrivée (monde).")]
        [SerializeField] private Vector3 endPosition;
        [Tooltip("Temps pour aller d'un bout à l'autre (s).")]
        [SerializeField] private float travelTime = 4f;
        [Tooltip("Pause à chaque extrémité (s).")]
        [SerializeField] private float waitTime = 0.5f;

        [Header("Obstacles Forest (anti-traversée)")]
        [Tooltip("Si vrai, la plateforme rebrousse chemin en heurtant un obstacle Forest.")]
        [SerializeField] private bool blockOnForestObstacles = true;
        [Tooltip("Marge de sécurité : s'arrête un peu AVANT l'obstacle (m).")]
        [SerializeField] private float obstacleSkin = 0.08f;

        // Fragments d'objet Forest considérés comme des murs (le terrain n'en fait pas partie).
        private static readonly string[] IgnoredForestTerrain =
        {
            "Ground", "Grass", "Water", "River", "Smoke", "Fire", "Pixie", "Coin"
        };

        private Vector3 startPosition;
        private float progress;      // avancement normalisé 0..1
        private int direction = 1;   // +1 vers l'arrivée, -1 vers le départ
        private float waitTimer;

        [Header("Transport du joueur")]
        [Tooltip("Marge horizontale autour de la plateforme pour détecter le joueur (m).")]
        [SerializeField] private float rideEdgeMargin = 0.35f;
        [Tooltip("Distance max sous le joueur pour trouver le support (m).")]
        [SerializeField] private float rideDetectDistance = 3f;

        private CharacterController riderController;
        private Transform playerTransform;
        private Vector3 previousPosition;

        // --- Transport "chaussure" -------------------------------------------
        // Offset monde du joueur par rapport à la plateforme, mémorisé quand il monte.
        // On le met à jour quand le joueur marche, et on l'applique sinon (zéro glissement).
        private bool riding;
        private Vector3 rideOffset;
        private Vector3 previousPlayerPosition;

        private void Start()
        {
            startPosition = transform.position;
            previousPosition = startPosition;

            playerTransform = FindPlayerTransform();
            if (playerTransform != null)
                riderController = playerTransform.GetComponent<CharacterController>();
        }

        /// <summary>
        /// Trouve le joueur : tag "Player" sinon nom "PlayerArmature"
        /// (le prefab Starter Assets peut arriver sans tag dans la scène).
        /// </summary>
        private static Transform FindPlayerTransform()
        {
            GameObject go = null;
            try { go = GameObject.FindGameObjectWithTag("Player"); }
            catch { go = null; }
            if (go != null) return go.transform;

            GameObject byName = GameObject.Find("PlayerArmature");
            return byName != null ? byName.transform : null;
        }

        private void Update()
        {
            if (travelTime <= 0f) return;

            if (waitTimer > 0f)
            {
                waitTimer -= Time.deltaTime;
                return;
            }

            // Position suivante visée (sans encore la valider).
            float nextProgress = Mathf.Clamp01(progress + direction * Time.deltaTime / travelTime);
            Vector3 nextPosition = Vector3.Lerp(startPosition, endPosition, nextProgress);
            Vector3 motion = nextPosition - transform.position;

            // Sonde : un obstacle Forest barre-t-il la route ?
            if (blockOnForestObstacles && motion.sqrMagnitude > 1e-8f && IsBlocked(motion))
            {
                // On ne traverse pas : demi-tour + petite pause.
                direction = -direction;
                waitTimer = waitTime;
                return;
            }

            progress = nextProgress;

            if (progress >= 1f)
            {
                progress = 1f;
                direction = -1;
                waitTimer = waitTime;
            }
            else if (progress <= 0f)
            {
                progress = 0f;
                direction = 1;
                waitTimer = waitTime;
            }

            transform.position = Vector3.Lerp(startPosition, endPosition, progress);
        }

        private void LateUpdate()
        {
            // Le joueur est-il posé sur la plateforme ? (détection raycast fiable)
            bool onPlatform = IsStandingOnPlatform();

            // --- Transport type "chaussure" --------------------------------------
            // Tant que le joueur est posé dessus, on l'asservit à la plateforme :
            //   cible = plateforme + offset. On corrige tout écart (glissement),
            //   SAUF quand le joueur marche (WASD) ou saute : l'offset suit alors.
            bool walking = ReadWalkInput();
            float playerDY = playerTransform.position.y - previousPlayerPosition.y;
            bool jumping = playerDY > 0.02f;   // le joueur monte -> il saute

            if (onPlatform && !jumping)
            {
                if (!riding)
                {
                    // Le joueur vient de monter : on mémorise sa place sur la plateforme.
                    rideOffset = playerTransform.position - transform.position;
                    riding = true;
                }
                else if (walking)
                {
                    // Il marche sur la plateforme : on déplace l'offset avec lui.
                    Vector3 walked = playerTransform.position - previousPlayerPosition;
                    rideOffset += new Vector3(walked.x, 0f, walked.z);
                }

                // Cible = même position sur la plateforme (qui a bougé pendant Update).
                Vector3 target = transform.position + rideOffset;
                Vector3 correction = target - playerTransform.position;

                // Hauteur : on suit la plateforme, on laisse la gravité faire le reste.
                correction.y = transform.position.y - previousPosition.y;

                MovePlayer(correction);
            }
            else
            {
                riding = false;
            }

            previousPosition = transform.position;
            previousPlayerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
        }

        /// <summary>Vrai si le joueur appuie sur une direction de marche (WASD/flèches).</summary>
        private static bool ReadWalkInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            return Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        }

        /// <summary>Déplace le joueur via son CharacterController (sinon le transform).</summary>
        private void MovePlayer(Vector3 motion)
        {
            if (motion.sqrMagnitude <= 0f) return;

            if (riderController != null && riderController.enabled)
            {
                riderController.Move(motion);
            }
            else if (playerTransform != null)
            {
                playerTransform.position += motion;
            }
        }

        /// <summary>
        /// Vrai si le joueur est posé sur la plateforme.
        /// Détection SANS trigger : on regarde TOUS les colliders sous le joueur
        /// (RaycastAll) et si l'un d'eux appartient à la plateforme, on considère
        /// qu'il est dessus. (Un simple Raycast ne suffit pas : un autre collider du
        /// décor peut être touché en premier.)
        /// </summary>
        private bool IsStandingOnPlatform()
        {
            if (playerTransform == null) return false;

            Vector3 origin = playerTransform.position + Vector3.up * 0.1f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _rayHits, rideDetectDistance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (IsOwnSolid(_rayHits[i].collider) && WithinHorizontalBounds(_rayHits[i].point))
                    return true;
            }
            return false;
        }

        private readonly RaycastHit[] _rayHits = new RaycastHit[8];

        /// <summary>
        /// Vrai si le point donné (position monde, typiquement les pieds du joueur)
        /// repose sur le collider solide de CETTE plateforme, dans son emprise.
        /// Utilisé par le RespawnManager pour détecter une "safe position" sur la plateforme.
        /// </summary>
        public bool IsOnPlatform(Vector3 worldPoint)
        {
            int count = Physics.RaycastNonAlloc(
                worldPoint + Vector3.up * 0.1f, Vector3.down, _rayHits,
                rideDetectDistance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (IsOwnSolid(_rayHits[i].collider) && WithinHorizontalBounds(_rayHits[i].point))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Position de repli par défaut : le CENTRE de la surface de la plateforme
        /// (un peu au-dessus), pour qu'un joueur qui tombe de la plateforme y revienne.
        /// </summary>
        public Vector3 GetRideCenter()
        {
            Bounds bounds = GetSolidBounds();
            return new Vector3(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z);
        }

        /// <summary>Vrai si le collider appartient à cette plateforme (elle-même ou un enfant).</summary>
        private bool IsOwnSolid(Collider collider)
        {
            if (collider == null) return false;
            Transform t = collider.transform;
            while (t != null)
            {
                if (t == transform) return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>Vrai si 'point' tombe dans l'emprise horizontale de la plateforme (+ marge).</summary>
        private bool WithinHorizontalBounds(Vector3 point)
        {
            Bounds bounds = GetSolidBounds();
            return point.x >= bounds.min.x - rideEdgeMargin && point.x <= bounds.max.x + rideEdgeMargin
                && point.z >= bounds.min.z - rideEdgeMargin && point.z <= bounds.max.z + rideEdgeMargin;
        }

        /// <summary>Bornes monde du collider solide (repli : boîte centrée sur le transform).</summary>
        private Bounds GetSolidBounds()
        {
            foreach (BoxCollider candidate in GetComponents<BoxCollider>())
            {
                if (!candidate.isTrigger) return candidate.bounds;
            }
            return new Bounds(transform.position, new Vector3(3f, 0.2f, 3f));
        }

        /// <summary>
        /// Vrai si la boîte de la plateforme, déplacée de 'motion', touche un obstacle Forest.
        /// On utilise un BoxCast (et non un raycast central) pour détecter les obstacles
        /// même sur les bords de la plateforme.
        /// </summary>
        private bool IsBlocked(Vector3 motion)
        {
            Vector3 direction = motion.normalized;
            float distance = motion.magnitude + obstacleSkin;

            Vector3 castCenter;
            Vector3 halfExtents;

            // On prend le BoxCollider SOLIDE (pas le trigger de détection du joueur).
            BoxCollider box = null;
            foreach (BoxCollider candidate in GetComponents<BoxCollider>())
            {
                if (!candidate.isTrigger) { box = candidate; break; }
            }
            if (box == null) box = GetComponent<BoxCollider>();

            if (box != null)
            {
                Bounds bounds = box.bounds;
                castCenter = bounds.center;
                halfExtents = bounds.extents; // déjà à l'échelle monde
            }
            else
            {
                castCenter = transform.position;
                halfExtents = new Vector3(0.5f, 0.1f, 0.5f);
            }

            RaycastHit[] hits = Physics.BoxCastAll(
                castCenter, halfExtents, direction, transform.rotation,
                distance, ~0, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                if (IsForestObstacle(hit.collider)) return true;
            }

            return false;
        }

        /// <summary>
        /// Un collider est un "obstacle Forest" si lui-même ou un de ses parents porte un nom
        /// commençant par "Forest", SANS être du terrain (sol/herbe/eau). Cela évite de bloquer
        /// la plateforme sur le sol ou la plateforme de départ.
        /// </summary>
        private static bool IsForestObstacle(Collider collider)
        {
            Transform t = collider.transform;
            while (t != null)
            {
                string n = t.name;
                if (n.StartsWith("Forest", StringComparison.OrdinalIgnoreCase))
                {
                    return !IsIgnoredTerrain(n);
                }
                t = t.parent;
            }
            return false;
        }

        private static bool IsIgnoredTerrain(string name)
        {
            foreach (string fragment in IgnoredForestTerrain)
            {
                if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>Fixe dynamiquement la destination (utilisé à l'import JSON).</summary>
        public void Configure(Vector3 destination, float travelSeconds)
        {
            endPosition = destination;
            travelTime = Mathf.Max(0.01f, travelSeconds);
        }
    }
}