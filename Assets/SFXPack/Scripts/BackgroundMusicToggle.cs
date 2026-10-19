using UnityEngine;
using UnityEngine.UI;

namespace Timbeaux.SFXPack
{
    public class BackgroundMusicToggle : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;

        [Header("Button")]
        [SerializeField] private Button musicButton;
        [SerializeField] private Image buttonImage;

        [Header("Volume Source")]
        [SerializeField] private Slider volumeSlider;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color mutedColor = Color.gray;

        private bool isMuted;

        private void Awake()
        {
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
            }

            if (musicButton != null)
            {
                musicButton.onClick.AddListener(ToggleMusic);
            }

            if (volumeSlider != null && musicSource != null)
            {
                musicSource.volume = volumeSlider.value;
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }

            isMuted = false;
            UpdateVisual();
        }

        private void ToggleMusic()
        {
            isMuted = !isMuted;

            if (musicSource != null)
            {
                if (isMuted)
                {
                    musicSource.Stop();
                }
                else
                {
                    musicSource.Play();
                }
            }

            UpdateVisual();
        }

        private void SetVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = volume;
            }
        }

        private void UpdateVisual()
        {
            if (buttonImage != null)
            {
                buttonImage.color = isMuted ? mutedColor : normalColor;
            }
        }
    }
}