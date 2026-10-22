using StarterAssets;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Projectile tiré par le piège Lanceur (PC-TRA-T2).
    /// Avance tout droit ; au moindre impact, il renvoie le joueur au dernier
    /// point de réapparition (via RespawnManager) puis se détruit.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrapProjectile : MonoBehaviour
    {
        private bool initialized;

        /// <summary>Démarre la vie du projectile (auto-destruction après 'lifetime' s).</summary>
        public void Initialize(float lifetime)
        {
            initialized = true;
            Destroy(gameObject, lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other.gameObject);
        }

        private void HandleHit(GameObject other)
        {
            if (!initialized) return;

            // Le joueur est identifié par son ThirdPersonController (dans ses parents).
            if (other.GetComponentInParent<ThirdPersonController>() != null)
            {
                // Tir encaissé = élimination, fin de partie immédiate.
                if (RaceManager.Instance != null)
                {
                    RaceManager.Instance.FailRace("Éliminé !");
                }
                else if (RespawnManager.Instance != null)
                {
                    // Sécurité : scène sans RaceManager (test isolé) -> respawn.
                    RespawnManager.Instance.Respawn();
                }
            }

            // Le projectile disparaît au premier contact (joueur, mur, sol...).
            Destroy(gameObject);
        }
    }
}
