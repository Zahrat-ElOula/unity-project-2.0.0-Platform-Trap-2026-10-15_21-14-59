using System.Reflection;
using StarterAssets;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Piège Bumper (jalon PC-TRA-T1).
    /// Quand le joueur entre dans le trigger, il est projeté dans la direction
    /// opposée au point de contact (horizontal), avec un léger biais vertical.
    ///
    /// Un CharacterController n'a pas de "vitesse" : on ne peut pas lui donner une
    /// impulsion en une seule frame aux hautes fréquences d'images. On applique donc
    /// une poussée CONTINUE pendant 'pushDuration' secondes (course de recul).
    /// </summary>
    public class BumperTrap : MonoBehaviour
    {
        [Header("Impulsion")]
        [Tooltip("Vitesse de recul appliquée pendant la poussée (m/s).")]
        [SerializeField] private float bumpForce = 14f;
        [Tooltip("Durée de la poussée continue (s).")]
        [SerializeField] private float pushDuration = 0.35f;
        [Tooltip("Biais vertical vers le haut (0 = purement horizontal).")]
        [SerializeField] private float upBias = 0.4f;
        [Tooltip("Délai minimal entre deux bumps (s).")]
        [SerializeField] private float cooldown = 0.4f;

        private FieldInfo verticalVelocityField;
        private float nextTime;

        private CharacterController rider;
        private ThirdPersonController riderController;
        private Vector3 pushDirection;
        private float pushEndTime;
        private bool pushing;

        private void Awake()
        {
            verticalVelocityField = typeof(ThirdPersonController)
                .GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextTime || pushing) return;

            ThirdPersonController thirdPerson = other.GetComponentInParent<ThirdPersonController>();
            if (thirdPerson == null) return;

            CharacterController cc = thirdPerson.GetComponent<CharacterController>();
            if (cc == null) return;

            // Direction : s'éloigner du POINT DE CONTACT par rapport au centre du bumper.
            // On utilise le point de contact (point le plus proche de l'empreinte du
            // joueur sur le bumper) plutôt que la simple position du joueur.
            Vector3 bumperCenter = transform.position + transform.up * (transform.localScale.y * 0.5f);
            Vector3 contactPoint = other.ClosestPoint(bumperCenter);
            contactPoint.y = 0f;
            bumperCenter.y = 0f;

            Vector3 dir = contactPoint - bumperCenter;
            if (dir.sqrMagnitude < 1e-4f)
            {
                dir = thirdPerson.transform.position - transform.position;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            dir.Normalize();

            // Biais vertical : on force un VRAI bond. Le ThirdPersonController remet
            // _verticalVelocity à -2 tant qu'il se croit au sol : il faut donc lui
            // dire qu'il n'est plus "Grounded" pour que le bond soit conservé.
            thirdPerson.Grounded = false;
            if (verticalVelocityField != null)
            {
                verticalVelocityField.SetValue(thirdPerson, upBias * bumpForce);
            }

            // Poussée CONTINUE : on stocke tout et on applique dans Update().
            rider = cc;
            riderController = thirdPerson;
            pushDirection = dir;
            pushEndTime = Time.time + pushDuration;
            pushing = true;

            // On empêche le trigger de re-déclencher pendant le recul.
            nextTime = Time.time + pushDuration + cooldown;

            Debug.Log("[BumperTrap] trigger -> poussée dans " + dir, this);
        }

        private void Update()
        {
            if (!pushing) return;

            if (rider == null || !rider.enabled || Time.time >= pushEndTime)
            {
                pushing = false;
                rider = null;
                riderController = null;
                return;
            }

            // Déplacement horizontal continu pendant la durée de poussée.
            rider.Move(pushDirection * bumpForce * Time.deltaTime);

            // Biais vertical entretenu : on garde le joueur en l'air (sinon il
            // retombe aussitôt) tant que la poussée dure.
            if (riderController != null && verticalVelocityField != null)
            {
                riderController.Grounded = false;
                verticalVelocityField.SetValue(riderController, upBias * bumpForce);
            }
        }
    }
}
