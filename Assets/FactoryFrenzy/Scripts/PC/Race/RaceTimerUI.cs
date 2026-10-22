using TMPro;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Affiche le chrono de la course en direct (HUD), à l'écran pendant la partie.
    /// Ce script ne calcule rien : il LIT simplement RaceTime du RaceManager
    /// (source de vérité unique) et l'écrit dans un texte UI chaque frame.
    /// </summary>
    public class RaceTimerUI : MonoBehaviour
    {
        [Tooltip("Texte UI qui affiche le temps (ex : 'Temps : 12.34').")]
        [SerializeField] private TMP_Text timerText;

        private void Update()
        {
            if (timerText == null)
            {
                return;
            }

            // Une fois la partie terminée (arrivée ou élimination), on ne
            // réécrit plus le texte : ça écraserait le message de fin.
            if (RaceManager.Instance != null && RaceManager.Instance.IsOver)
            {
                return;
            }

            float time = RaceManager.Instance != null ? RaceManager.Instance.RaceTime : 0f;
            timerText.text = $"Temps : {time:0.00}";
        }
    }
}
