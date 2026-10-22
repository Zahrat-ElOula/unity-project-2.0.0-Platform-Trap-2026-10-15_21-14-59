using StarterAssets;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Gestion de la chute (jalon PC-PLA-P4).
    ///
    /// Comportement : quand le joueur tombe (seuil de hauteur OU kill zone triggée),
    /// il est replacé à sa DERNIÈRE POSITION VALIDE (le dernier endroit où il était
    /// posé sur un sol), et non au point de départ.
    ///
    /// La dernière position valide est mise à jour en continu tant que le joueur
    /// est bien au sol (petit raycast vers le bas).
    /// </summary>
    public class RespawnManager : MonoBehaviour
    {
        public static RespawnManager Instance { get; private set; }

        [Header("Réglages")]
        [Tooltip("Le joueur est considéré en chute si sa hauteur (Y) descend sous ce seuil.")]
        [SerializeField] private float fallThreshold = -20f;

        [Tooltip("Utilise aussi un trigger 'kill zone' (OnTriggerEnter) posé sous le niveau.")]
        [SerializeField] private bool useKillTrigger = true;

        [Tooltip("Distance sous le joueur pour considérer qu'il est posé sur un sol (m).")]
        [SerializeField] private float groundCheckDistance = 0.6f;

        [Header("Références")]
        [Tooltip("Le joueur (Transform). Vidé = recherché automatiquement.")]
        [SerializeField] private Transform player;

        [Tooltip("Point de repli si aucune position valide n'a encore été mémorisée.")]
        [SerializeField] private Transform respawnPoint;

        private CharacterController controller;
        private float respawnCooldown;

        // Dernière position où le joueur était bien posé sur un sol.
        private bool hasSafePosition;
        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;

        // Si la dernière position valide était sur une plateforme mouvante,
        // on retient laquelle : en cas de chute, on revient à son CENTRE (elle bouge).
        private FactoryFrenzy.PC.MovingPlatform lastSafePlatform;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Recherche automatique du joueur si non câblé.
            if (player == null)
            {
                ThirdPersonController thirdPerson = FindObjectOfType<ThirdPersonController>();
                if (thirdPerson != null) player = thirdPerson.transform;
            }

            if (player != null) controller = player.GetComponent<CharacterController>();

            // Position de départ = repli par défaut.
            if (player != null)
            {
                lastSafePosition = player.position;
                lastSafeRotation = player.rotation;
                hasSafePosition = true;
            }

            if (respawnPoint != null)
            {
                lastSafePosition = respawnPoint.position;
                lastSafeRotation = respawnPoint.rotation;
            }
        }

        private void Update()
        {
            if (respawnCooldown > 0f) respawnCooldown -= Time.deltaTime;

            if (player == null) return;

            // 1) Mémorise la position tant que le joueur est posé sur un sol.
            bool onGround = Physics.Raycast(
                player.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit,
                groundCheckDistance + 0.2f, ~0, QueryTriggerInteraction.Ignore);

            if (onGround && player.position.y >= fallThreshold)
            {
                // Le support est-il une plateforme mouvante ?
                FactoryFrenzy.PC.MovingPlatform platform =
                    hit.collider != null ? hit.collider.GetComponentInParent<FactoryFrenzy.PC.MovingPlatform>() : null;

                if (platform != null)
                {
                    // Sur une plateforme : le repli sera son centre (elle bouge).
                    lastSafePlatform = platform;
                }
                else
                {
                    lastSafePlatform = null;
                    lastSafePosition = player.position;
                    lastSafeRotation = player.rotation;
                }
                hasSafePosition = true;
            }

            // 2) Chute détectée -> retour à la dernière position valide.
            if (player.position.y < fallThreshold)
            {
                RecallToSafePosition();
            }
        }

        /// <summary>
        /// Kill zone : quand le joueur entre dans le volume trigger, on le replace
        /// à la dernière position valide.
        /// (Le trigger est sur CE GameObject ; c'est donc ici que OnTriggerEnter arrive.)
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!useKillTrigger) return;
            if (IsPlayer(other)) RecallToSafePosition();
        }

        private bool IsPlayer(Collider other)
        {
            if (other == null) return false;
            if (player != null && other.transform == player) return true;
            // Le collider peut être un enfant du joueur : on vérifie la racine.
            if (player != null && other.transform.root == player.root) return true;
            return other.CompareTag("Player");
        }

        /// <summary>Déplace le point de repli (conservé pour compatibilité).</summary>
        public void SetCheckpoint(Transform point)
        {
            if (point != null)
            {
                lastSafePlatform = null;
                lastSafePosition = point.position;
                lastSafeRotation = point.rotation;
                hasSafePosition = true;
            }
        }

        /// <summary>Replace le joueur à la dernière position valide (vélocité remise à zéro).</summary>
        public void RecallToSafePosition()
        {
            if (player == null || !hasSafePosition) return;
            if (respawnCooldown > 0f) return;   // évite le spam

            respawnCooldown = 0.3f;

            // Cible : le centre de la plateforme si on venait d'une plateforme,
            // sinon la dernière position valide au sol.
            Vector3 target = lastSafePlatform != null
                ? lastSafePlatform.GetRideCenter()
                : lastSafePosition;
            Quaternion targetRot = lastSafePlatform != null
                ? player.rotation
                : lastSafeRotation;

            // Désactiver/réactiver le CharacterController remet sa vitesse interne à zéro.
            if (controller != null) controller.enabled = false;
            player.SetPositionAndRotation(target, targetRot);
            if (controller != null) controller.enabled = true;
        }

        /// <summary>Alias conservé : même comportement que RecallToSafePosition().</summary>
        public void Respawn() => RecallToSafePosition();
    }
}
