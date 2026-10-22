using StarterAssets;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Piège Lanceur (jalon PC-TRA-T2).
    /// Machine à états : Recherche -> Orientation -> Tir -> Recharge -> ...
    ///   1. Recherche   : la tourelle tourne lentement et cherche le joueur
    ///                    (distance + ligne de vue par Raycast).
    ///   2. Orientation : la tourelle pivote vers le joueur à vitesse limitée.
    ///   3. Tir         : alignée sur le joueur -> un projectile est instancié.
    ///   4. Recharge    : temps d'attente avant de pouvoir tirer à nouveau.
    /// </summary>
    public class ShooterTrap : MonoBehaviour
    {
        public enum State { Searching, Aiming, Shooting, Cooldown }

        [Header("Détection")]
        [Tooltip("Distance max de détection du joueur (m).")]
        [SerializeField] private float detectRadius = 15f;

        [Header("Orientation")]
        [Tooltip("Objet qui pivote pour viser. Vidé = ce GameObject.")]
        [SerializeField] private Transform turret;
        [Tooltip("Vitesse de rotation pendant la recherche (degrés/s).")]
        [SerializeField] private float idleSpinSpeed = 25f;
        [Tooltip("Vitesse d'orientation vers le joueur (degrés/s).")]
        [SerializeField] private float aimSpeed = 180f;
        [Tooltip("Écart d'angle (degrés) toléré pour tirer.")]
        [SerializeField] private float fireAngle = 12f;

        [Header("Tir")]
        [Tooltip("Point de départ des projectiles (à l'avant du canon).")]
        [SerializeField] private Transform muzzle;
        [Tooltip("Vitesse du projectile (m/s).")]
        [SerializeField] private float projectileSpeed = 14f;
        [Tooltip("Délai entre deux tirs (s).")]
        [SerializeField] private float fireInterval = 1.2f;
        [Tooltip("Durée de vie du projectile avant auto-destruction (s).")]
        [SerializeField] private float projectileLifetime = 4f;
        [Tooltip("Diamètre du projectile (m).")]
        [SerializeField] private float projectileSize = 0.25f;

        [Header("Références")]
        [Tooltip("Le joueur. Vidé = recherché automatiquement.")]
        [SerializeField] private Transform player;

        public State Current { get; private set; } = State.Searching;
        private float cooldownTimer;

        private void Reset()
        {
            turret = transform;
        }

        private void Awake()
        {
            if (turret == null) turret = transform;
        }

        private void Start()
        {
            if (player == null)
            {
                ThirdPersonController thirdPerson = FindObjectOfType<ThirdPersonController>();
                if (thirdPerson != null) player = thirdPerson.transform;
            }
        }

        private void Update()
        {
            if (player == null)
            {
                Current = State.Searching;
                Spin();
                return;
            }

            bool inRange = Vector3.Distance(transform.position, player.position) <= detectRadius;
            bool canSee = inRange && HasLineOfSight();

            switch (Current)
            {
                case State.Searching:
                    Spin();
                    if (canSee) Current = State.Aiming;
                    break;

                case State.Aiming:
                    if (!canSee) { Current = State.Searching; break; }
                    if (AimAt(player.position, Time.deltaTime)) Current = State.Shooting;
                    break;

                case State.Shooting:
                    Fire();
                    cooldownTimer = fireInterval;
                    Current = State.Cooldown;
                    break;

                case State.Cooldown:
                    cooldownTimer -= Time.deltaTime;
                    if (cooldownTimer <= 0f) Current = State.Aiming;
                    break;
            }
        }

        /// <summary>Rotation lente de recherche.</summary>
        private void Spin()
        {
            turret.Rotate(Vector3.up, idleSpinSpeed * Time.deltaTime, Space.World);
        }

        /// <summary>Oriente la tourelle vers la cible (à l'horizontale) ; vrai si alignée.</summary>
        private bool AimAt(Vector3 target, float deltaTime)
        {
            Vector3 direction = target - turret.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return true;

            direction.Normalize();
            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            turret.rotation = Quaternion.RotateTowards(turret.rotation, desired, aimSpeed * deltaTime);

            return Vector3.Angle(turret.forward, direction) <= fireAngle;
        }

        /// <summary>Vrai si la tourelle "voit" le joueur (aucun obstacle entre les deux).</summary>
        private bool HasLineOfSight()
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.5f;
            Vector3 dest = player.position + Vector3.up * 1.0f;
            Vector3 dir = dest - origin;

            float distance = dir.magnitude;
            if (distance < 0.01f) return true;

            if (Physics.Raycast(origin, dir / distance, out RaycastHit hit, distance + 0.2f))
            {
                return hit.collider.GetComponentInParent<ThirdPersonController>() != null;
            }
            return true;
        }

        /// <summary>Instancie un projectile et le propulse vers le joueur.</summary>
        private void Fire()
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + transform.forward * 0.5f;
            Vector3 target = player.position + Vector3.up * 1.0f;
            Vector3 direction = (target - origin).normalized;

            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "TrapProjectile";
            projectile.transform.position = origin;
            projectile.transform.localScale = Vector3.one * projectileSize;

            Renderer renderer = projectile.GetComponent<Renderer>();
            renderer.material.color = new Color(1f, 0.35f, 0.05f);

            SphereCollider collider = projectile.GetComponent<SphereCollider>();
            collider.isTrigger = false;

            Rigidbody rigidbody = projectile.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.velocity = direction * projectileSpeed;

            // Ignore les collisions entre le projectile et le piège lui-même.
            foreach (Collider trapCollider in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(collider, trapCollider);
            }

            projectile.AddComponent<TrapProjectile>().Initialize(projectileLifetime);
        }
    }
}
