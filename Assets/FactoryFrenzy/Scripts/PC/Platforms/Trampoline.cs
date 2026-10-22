using System.Reflection;
using StarterAssets;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Plateforme trampoline (jalon PC-PLA-P6).
    /// Un volume trigger couvre le dessus : quand le joueur entre dedans,
    /// on lui donne une vitesse verticale vers le haut (équivalente à un saut
    /// de 'bounceHeight' mètres), puis sa propre gravité le fait retomber.
    ///
    /// Le contrôleur du joueur (Starter Assets) utilise un CharacterController
    /// et stocke sa vitesse verticale dans le champ privé '_verticalVelocity'.
    /// Comme ce champ n'est pas public, on le modifie par réflexion — sans
    /// modifier le script du Starter Assets.
    /// </summary>
    public class Trampoline : MonoBehaviour
    {
        [Tooltip("Hauteur du rebond (m).")]
        [SerializeField] private float bounceHeight = 5f;

        [Tooltip("Délai minimal entre deux rebonds (s).")]
        [SerializeField] private float bounceCooldown = 0.15f;

        private FieldInfo verticalVelocityField;
        private float nextBounceTime;

        private void Awake()
        {
            verticalVelocityField = typeof(ThirdPersonController)
                .GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

            if (verticalVelocityField == null)
            {
                Debug.LogWarning("[FF-Repair] Champ '_verticalVelocity' introuvable : trampoline inactif.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (verticalVelocityField == null) return;
            if (Time.time < nextBounceTime) return;

            ThirdPersonController thirdPerson = other.GetComponentInParent<ThirdPersonController>();
            if (thirdPerson == null) return;

            verticalVelocityField.SetValue(thirdPerson, ComputeBounceVelocity(thirdPerson));
            nextBounceTime = Time.time + bounceCooldown;
        }

        /// <summary>Vitesse verticale pour atteindre 'bounceHeight' : v = sqrt(h * -2 * g).</summary>
        private float ComputeBounceVelocity(ThirdPersonController player)
        {
            return Mathf.Sqrt(bounceHeight * -2f * player.Gravity);
        }
    }
}
