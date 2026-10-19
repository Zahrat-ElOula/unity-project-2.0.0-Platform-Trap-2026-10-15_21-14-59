using UnityEngine;
using UnityEngine.UI;

namespace Timbeaux.SFXPack
{
    public class SFXManager : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource previewAudioSource;

        [Header("Controls")]
        [SerializeField] private Toggle loopToggle;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Slider pitchSlider;

        [Header("Category Pages")]
        [SerializeField] private GameObject interactionPage;
        [SerializeField] private GameObject uiPage;
        [SerializeField] private GameObject ambientPage;
        [SerializeField] private GameObject weatherPage;

        [Header("Category Button Images")]
        [SerializeField] private Image interactionButtonImage;
        [SerializeField] private Image uiButtonImage;
        [SerializeField] private Image ambientButtonImage;
        [SerializeField] private Image weatherButtonImage;

        [Header("Category Button Colors")]
        [SerializeField] private Color activeButtonColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color inactiveButtonColor = Color.white;

        private void Awake()
        {
            if (volumeSlider != null && previewAudioSource != null)
            {
                previewAudioSource.volume = volumeSlider.value;
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }

            if (pitchSlider != null && previewAudioSource != null)
            {
                previewAudioSource.pitch = RemapPitch(pitchSlider.value);
                pitchSlider.onValueChanged.AddListener(SetPitch);
            }

            if (loopToggle != null && previewAudioSource != null)
            {
                previewAudioSource.loop = loopToggle.isOn;
                loopToggle.onValueChanged.AddListener(SetLoop);
            }

            ShowInteractionPage();
        }

        public void PlaySound(AudioClip clip)
        {
            if (clip == null || previewAudioSource == null)
            {
                return;
            }

            previewAudioSource.Stop();
            previewAudioSource.clip = clip;
            previewAudioSource.loop = loopToggle != null && loopToggle.isOn;
            previewAudioSource.pitch = pitchSlider != null ? RemapPitch(pitchSlider.value) : 1f;
            previewAudioSource.Play();
        }

        public void StopSound()
        {
            if (previewAudioSource == null)
            {
                return;
            }

            previewAudioSource.Stop();
        }

        public void ShowInteractionPage()
        {
            ShowPage(interactionPage);
        }

        public void ShowUIPage()
        {
            ShowPage(uiPage);
        }

        public void ShowAmbientPage()
        {
            ShowPage(ambientPage);
        }

        public void ShowWeatherPage()
        {
            ShowPage(weatherPage);
        }

        private void ShowPage(GameObject pageToShow)
        {
            SetPageActive(interactionPage, pageToShow);
            SetPageActive(uiPage, pageToShow);
            SetPageActive(ambientPage, pageToShow);
            SetPageActive(weatherPage, pageToShow);

            UpdateCategoryButtonVisuals(pageToShow);
            StopSound();
        }

        private void SetPageActive(GameObject page, GameObject pageToShow)
        {
            if (page != null)
            {
                page.SetActive(page == pageToShow);
            }
        }

        private void UpdateCategoryButtonVisuals(GameObject activePage)
        {
            SetCategoryButtonColor(interactionButtonImage, activePage == interactionPage);
            SetCategoryButtonColor(uiButtonImage, activePage == uiPage);
            SetCategoryButtonColor(ambientButtonImage, activePage == ambientPage);
            SetCategoryButtonColor(weatherButtonImage, activePage == weatherPage);
        }

        private void SetCategoryButtonColor(Image buttonImage, bool isActive)
        {
            if (buttonImage == null)
            {
                return;
            }

            buttonImage.color = isActive ? activeButtonColor : inactiveButtonColor;
        }

        private void SetVolume(float volume)
        {
            if (previewAudioSource != null)
            {
                previewAudioSource.volume = volume;
            }
        }

        private void SetPitch(float sliderValue)
        {
            if (previewAudioSource != null)
            {
                previewAudioSource.pitch = RemapPitch(sliderValue);
            }
        }

        private void SetLoop(bool loop)
        {
            if (previewAudioSource != null)
            {
                previewAudioSource.loop = loop;
            }
        }

        private float RemapPitch(float sliderValue)
        {
            if (sliderValue < 0.5f)
            {
                return Mathf.Lerp(0.5f, 1f, sliderValue / 0.5f);
            }
            else
            {
                return Mathf.Lerp(1f, 2f, (sliderValue - 0.5f) / 0.5f);
            }
        }
    }
}