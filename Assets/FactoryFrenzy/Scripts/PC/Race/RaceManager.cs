using System;
using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Chef d'orchestre de la course (jalon PC-PLA-P2, réutilisé par PC-PLA-P3).
    /// Gère une petite machine à états :
    ///   Countdown (décompte, joueur bloqué) -> Racing (chrono) -> Finished (arrivée).
    /// C'est le seul objet qui "sait" où en est la course ; les autres scripts
    /// (plateforme d'arrivée, pièges...) dialoguent avec lui via des événements.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        /// <summary>États possibles de la course.</summary>
        public enum RaceState
        {
            Countdown, // décompte en cours, le joueur ne peut pas bouger
            Racing,    // GO donné, le chrono tourne
            Finished,  // le joueur a atteint l'arrivée
            Failed     // le joueur a été éliminé (ex : touché par un tir)
        }

        /// <summary>Accès global simple (un seul gestionnaire de course par scène).</summary>
        public static RaceManager Instance { get; private set; }

        [Header("Réglages")]
        [Tooltip("Valeur de départ du décompte (3 -> affiche 3, 2, 1 puis GO).")]
        [SerializeField] private int countdownFrom = 3;

        [Header("Références")]
        [Tooltip("Contrôleur du joueur (Starter Assets). Il est désactivé pendant le décompte.")]
        [SerializeField] private ThirdPersonController playerController;

        [Tooltip("Texte UI (TextMeshPro) qui affiche le décompte.")]
        [SerializeField] private TMP_Text countdownText;

        /// <summary>État courant de la course (lecture seule pour l'extérieur).</summary>
        public RaceState State { get; private set; } = RaceState.Countdown;

        /// <summary>Temps écoulé depuis le GO, en secondes.</summary>
        public float RaceTime { get; private set; }

        /// <summary>Vrai si la partie est terminée (arrivée OU élimination).</summary>
        public bool IsOver => State == RaceState.Finished || State == RaceState.Failed;

        /// <summary>Déclenché quand le GO est donné (le joueur peut bouger).</summary>
        public event Action RaceStarted;

        /// <summary>Déclenché à l'arrivée, avec le temps final (utilisé en PC-PLA-P3).</summary>
        public event Action<float> RaceFinished;

        /// <summary>Déclenché quand le joueur est éliminé, avec le message d'échec.</summary>
        public event Action<string> RaceFailed;

        private void Awake()
        {
            // Singleton : un seul RaceManager actif, sinon on détruit le doublon.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Secours : si la référence n'a pas été câblée, on cherche le joueur automatiquement.
            if (playerController == null)
            {
                playerController = FindObjectOfType<ThirdPersonController>();
            }

            // Tant que le décompte n'est pas fini, le joueur est bloqué.
            SetPlayerLocked(true);
            StartCoroutine(CountdownRoutine());
        }

        /// <summary>Décompte 3 -> 2 -> 1 puis "GO !", en bloquant le joueur puis en le libérant.</summary>
        private IEnumerator CountdownRoutine()
        {
            for (int number = countdownFrom; number >= 1; number--)
            {
                SetCountdownText(number.ToString());
                yield return new WaitForSeconds(1f);
            }

            // GO : on libère le joueur et on démarre officiellement la course.
            SetCountdownText("GO !");
            SetPlayerLocked(false);
            State = RaceState.Racing;
            RaceStarted?.Invoke();

            // On laisse "GO !" s'afficher une seconde, puis on efface.
            yield return new WaitForSeconds(1f);
            SetCountdownText(string.Empty);
        }

        private void Update()
        {
            // Le chrono ne tourne QUE pendant la course.
            if (State == RaceState.Racing)
            {
                RaceTime += Time.deltaTime;
            }
        }

        /// <summary>
        /// Termine la course (appelé par la plateforme d'arrivée en PC-PLA-P3).
        /// Ignoré si la course n'a pas encore commencé ou est déjà finie.
        /// </summary>
        public void FinishRace()
        {
            if (State != RaceState.Racing) return;

            State = RaceState.Finished;
            SetPlayerLocked(true);
            SetCountdownText("Arrivée !");
            RaceFinished?.Invoke(RaceTime);
        }

        /// <summary>
        /// Élimine le joueur (ex : touché par le piège Lanceur).
        /// Ignoré si la course n'a pas commencé ou est déjà terminée.
        /// </summary>
        public void FailRace(string reason)
        {
            if (State != RaceState.Racing) return;

            State = RaceState.Failed;
            SetPlayerLocked(true);
            string message = string.IsNullOrEmpty(reason) ? "Éliminé !" : reason;
            SetCountdownText(message);
            RaceFailed?.Invoke(message);
        }

        /// <summary>Active/désactive le contrôle du joueur (bloque le déplacement).</summary>
        private void SetPlayerLocked(bool locked)
        {
            if (playerController != null)
            {
                playerController.enabled = !locked;
            }
        }

        /// <summary>Met à jour le texte du décompte (sans planter si non assigné).</summary>
        private void SetCountdownText(string value)
        {
            if (countdownText != null)
            {
                countdownText.text = value;
            }
        }
    }
}
