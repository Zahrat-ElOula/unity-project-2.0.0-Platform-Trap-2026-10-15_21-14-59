using StarterAssets;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// À placer sur un volume (collider "Is Trigger") de la plateforme d'arrivée.
    /// Quand le joueur entre dans la zone, on prévient le RaceManager (PC-PLA-P3).
    /// Aucun besoin de Tag : on reconnaît le joueur grâce à son ThirdPersonController,
    /// ce qui évite de dépendre de la config des Tags/Tags du projet.
    /// </summary>
    public class FinishPlatform : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Est-ce bien le joueur ? (le composant peut être sur un parent)
            if (other.GetComponentInParent<ThirdPersonController>() == null)
            {
                return;
            }

            // On délègue au chef d'orchestre : c'est lui qui connaît l'état de la course.
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.FinishRace();
            }
        }
    }
}
