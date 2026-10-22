using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Menu principal (lobby). Très simple : le bouton "Lancer la partie" appelle
    /// PlayGame(), qui charge la scène de jeu. Le nom de la scène est configurable
    /// dans l'Inspector (par défaut "Game_Level_01").
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("Nom de la scène de jeu à charger.")]
        [SerializeField] private string gameSceneName = "Game_Level_01";

        /// <summary>Charge la scène de jeu (appelé par le bouton du lobby).</summary>
        public void PlayGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>Ferme le jeu (utile pour un bouton Quitter).</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
