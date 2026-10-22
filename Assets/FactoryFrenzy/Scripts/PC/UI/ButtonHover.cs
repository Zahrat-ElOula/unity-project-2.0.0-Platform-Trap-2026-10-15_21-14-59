using UnityEngine;
using UnityEngine.EventSystems;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Agrandit légèrement un élément UI (bouton) au survol, revient à la normale sinon,
    /// et joue deux petits sons SYNTHÉTIQUES (aucun fichier audio externe) :
    /// un "tick" aigu au survol, un bip plus grave au clic.
    ///
    /// Implémente les interfaces d'événements UI de Unity : aucune modification
    /// du composant Button n'est nécessaire.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ButtonHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Survol (scale)")]
        [Tooltip("Facteur d'agrandissement au survol (1.1 = +10%).")]
        [SerializeField] private float hoverScale = 1.1f;

        [Header("Sons synthétiques")]
        [Tooltip("Joue un son au survol.")]
        [SerializeField] private bool playHoverSound = true;
        [Tooltip("Joue un son au clic.")]
        [SerializeField] private bool playClickSound = true;
        [Tooltip("Fréquence du son de survol (Hz).")]
        [SerializeField] private float hoverFrequency = 1200f;
        [Tooltip("Fréquence du son de clic (Hz).")]
        [SerializeField] private float clickFrequency = 400f;
        [Tooltip("Volume des deux sons (0..1).")]
        [SerializeField][Range(0f, 1f)] private float volume = 0.4f;

        private AudioSource audioSource;
        private AudioClip hoverClip;
        private AudioClip clickClip;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // son 2D (UI)

            hoverClip = BuildBeep("HoverBeep", hoverFrequency, 0.07f);
            clickClip = BuildBeep("ClickBeep", clickFrequency, 0.12f);
        }

        /// <summary>La souris entre sur le bouton : on agrandit (+ tick).</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = Vector3.one * hoverScale;

            if (playHoverSound && hoverClip != null)
                audioSource.PlayOneShot(hoverClip, volume);
        }

        /// <summary>La souris sort du bouton : on revient à la taille normale.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }

        /// <summary>Clic sur le bouton : bip plus grave.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (playClickSound && clickClip != null)
                audioSource.PlayOneShot(clickClip, volume);
        }

        /// <summary>
        /// Génère un petit bip sinusoïdal avec une enveloppe (attaque/déclin) pour
        /// éviter le "clic" numérique en début/fin.
        /// </summary>
        private static AudioClip BuildBeep(string name, float frequency, float duration)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                float envelope = Mathf.Min(1f, i / (sampleRate * 0.01f)) // attaque 10 ms
                               * Mathf.Clamp01(1f - (float)i / sampleCount); // déclin
                data[i] = wave * envelope * 0.6f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
