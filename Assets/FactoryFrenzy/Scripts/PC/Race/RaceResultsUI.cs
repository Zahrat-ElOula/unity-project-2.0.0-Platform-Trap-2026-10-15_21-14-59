using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Écran de résultats (PC-PLA-P3). Reste inactif pendant la course, puis s'affiche
    /// quand le RaceManager signale l'arrivée (événement RaceFinished). Il affiche le
    /// temps final et propose un bouton "Recommencer".
    ///
    /// IMPORTANT : ce script doit être sur un objet TOUJOURS ACTIF (ex : RaceManager),
    /// sinon son Start() ne s'exécuterait jamais tant que le panneau est masqué.
    /// </summary>
    public class RaceResultsUI : MonoBehaviour
    {
        [Header("Références UI")]
        [Tooltip("Le panneau (objet) à afficher à l'arrivée. Il démarre masqué.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Texte qui affiche le temps final.")]
        [SerializeField] private TMP_Text timeText;

        [Tooltip("Bouton pour rejouer la scène.")]
        [SerializeField] private Button restartButton;

        private void Start()
        {
            if (panel != null) panel.SetActive(false);

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }

            // Abonnement aux événements du RaceManager (découplage total).
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.RaceFinished += ShowResults;
                RaceManager.Instance.RaceFailed += ShowFailure;
            }
        }

        private void OnDestroy()
        {
            // Bon réflexe : se désabonner pour éviter les fuites/erreurs.
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.RaceFinished -= ShowResults;
                RaceManager.Instance.RaceFailed -= ShowFailure;
            }
        }

        /// <summary>Affiche le panneau en mode échec (élimination) + message.</summary>
        private void ShowFailure(string reason)
        {
            if (panel != null) panel.SetActive(true);
            if (timeText != null) timeText.text = string.IsNullOrEmpty(reason) ? "Éliminé !" : reason;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Affiche le panneau de résultats avec le temps écoulé.</summary>
        private void ShowResults(float time)
        {
            if (panel != null) panel.SetActive(true);
            if (timeText != null) timeText.text = $"Temps : {time:0.00} s";

            // Le curseur doit être visible/libre pour pouvoir cliquer sur le bouton.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Relance la scène courante (nouvelle tentative).</summary>
        private void Restart()
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }
}
