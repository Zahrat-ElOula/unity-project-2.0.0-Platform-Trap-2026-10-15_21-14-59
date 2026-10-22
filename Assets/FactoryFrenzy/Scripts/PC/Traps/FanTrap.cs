using System.Reflection;
using StarterAssets;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Piège Ventilateur (jalon PC-TRA-T3).
    /// Crée une zone d'air qui pousse le joueur dans la direction donnée par le
    /// transform (forward par défaut) avec une force constante.
    ///
    /// Effet visuel : un souffle de particules blanches (poussière/fumée) aligné
    /// sur la direction de poussée. Les particules ne s'activent que lorsque le
    /// joueur est DANS la zone (un vrai ventilateur ne souffle pas à vide).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FanTrap : MonoBehaviour
    {
        [Header("Poussée")]
        [Tooltip("Direction de poussée (monde). Par défaut = forward de ce GameObject.")]
        [SerializeField] private Vector3 pushDirection = Vector3.forward;
        [Tooltip("Vitesse de poussée appliquée au joueur (m/s).")]
        [SerializeField] private float pushForce = 3.5f;
        [Tooltip("Ajoute un léger effet vers le haut (0 = purement horizontal).")]
        [SerializeField] private float liftBias = 0f;

        [Header("Particules")]
        [Tooltip("Active le souffle de particules.")]
        [SerializeField] private bool showParticles = true;
        [Tooltip("Nombre de particules émises par seconde.")]
        [SerializeField] private float particleRate = 60f;
        [Tooltip("Couleur des particules.")]
        [SerializeField] private Color particleColor = Color.white;
        [Tooltip("Vitesse des particules (m/s).")]
        [SerializeField] private float particleSpeed = 4f;
        [Tooltip("Durée de vie des particules (s).")]
        [SerializeField] private float particleLifetime = 1.2f;

        private BoxCollider fanZone;
        private FieldInfo verticalVelocityField;
        private Transform player;
        private CharacterController playerController;
        private bool playerInside;

        private ParticleSystem particles;

        private void Awake()
        {
            fanZone = GetComponent<BoxCollider>();
            if (fanZone != null) fanZone.isTrigger = true;

            pushDirection.Normalize();

            verticalVelocityField = typeof(ThirdPersonController)
                .GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void Start()
        {
            if (showParticles) BuildParticles();
        }

        private void OnTriggerEnter(Collider other)
        {
            ThirdPersonController thirdPerson = other.GetComponentInParent<ThirdPersonController>();
            if (thirdPerson == null) return;

            player = thirdPerson.transform;
            playerController = thirdPerson.GetComponent<CharacterController>();
            playerInside = true;

            // Le joueur entre dans le souffle -> on démarre les particules.
            if (particles != null) particles.Play();
        }

        private void OnTriggerExit(Collider other)
        {
            ThirdPersonController thirdPerson = other.GetComponentInParent<ThirdPersonController>();
            if (thirdPerson != null && thirdPerson.transform == player)
            {
                playerInside = false;
                player = null;
                playerController = null;

                // Le joueur sort -> on arrête le souffle.
                if (particles != null) particles.Stop();
            }
        }

        private void Update()
        {
            if (!playerInside || player == null || playerController == null) return;
            if (!playerController.enabled) return;

            // Poussée continue horizontale (+ option vers le haut).
            Vector3 push = pushDirection * pushForce * Time.deltaTime;
            playerController.Move(push);

            if (liftBias > 0f && verticalVelocityField != null)
            {
                verticalVelocityField.SetValue(
                    player.GetComponent<ThirdPersonController>(), liftBias * pushForce);
            }
        }

        /// <summary>Construit un système de particules blanches orienté vers la poussée.</summary>
        private void BuildParticles()
        {
            GameObject go = new GameObject("FanParticles");
            go.transform.SetParent(transform, false);

            particles = go.AddComponent<ParticleSystem>();
            particles.Stop();

            // On oriente l'émetteur le long de la direction de poussée.
            go.transform.rotation = Quaternion.LookRotation(pushDirection, Vector3.up);

            ParticleSystem.MainModule main = particles.main;
            main.startColor = particleColor;
            main.startSpeed = particleSpeed;
            main.startLifetime = particleLifetime;
            main.startSize = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = particleRate;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            // Émetteur = face avant du ventilateur (volume de la zone d'air).
            Vector3 size = fanZone != null ? fanZone.size : new Vector3(2f, 1f, 4f);
            Vector3 center = fanZone != null ? fanZone.center : Vector3.zero;
            go.transform.localPosition = center - pushDirection * (size.z * 0.5f);

            shape.scale = new Vector3(
                Mathf.Max(size.x, 0.5f) * 0.8f,
                Mathf.Max(size.y, 0.5f) * 0.8f,
                0.05f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.7f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverTime = particles.sizeOverLifetime;
            sizeOverTime.enabled = true;
            sizeOverTime.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(1f, 1.4f)));

            ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, pushDirection.normalized * 1.5f);
        }
    }
}
