using UnityEngine;
using UnityEngine.UI;

namespace Timbeaux.SFXPack
{
    [RequireComponent(typeof(Image))]
    public class StarTwinkle : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite[] starSprites;

        [Header("Size")]
        [SerializeField] private float minSize = 6f;
        [SerializeField] private float maxSize = 18f;

        [Header("Alpha")]
        [SerializeField] private float minAlpha = 0.25f;
        [SerializeField] private float maxAlpha = 1f;

        [Header("Twinkle")]
        [SerializeField] private float minTwinkleSpeed = 0.75f;
        [SerializeField] private float maxTwinkleSpeed = 2.5f;
        [SerializeField] private bool randomizeOnStart = true;

        [Header("Rare Color Blink")]
        [SerializeField] private bool allowRareColorBlink = true;
        [SerializeField] private float rareColorBlinkChance = 0.08f;
        [SerializeField] private float colorBlinkSpeed = 6f;

        private Image image;
        private RectTransform rectTransform;

        private float baseAlpha;
        private float twinkleSpeed;
        private float twinkleOffset;
        private bool useColorBlink;
        private Color baseColor;

        private void Awake()
        {
            image = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();

            if (randomizeOnStart)
            {
                RandomizeStar();
            }

            baseColor = image.color;
            baseAlpha = baseColor.a;
            twinkleSpeed = Random.Range(minTwinkleSpeed, maxTwinkleSpeed);
            twinkleOffset = Random.Range(0f, 100f);

            useColorBlink = allowRareColorBlink && Random.value <= rareColorBlinkChance;
        }

        private void Update()
        {
            float pulse = Mathf.Sin((Time.time + twinkleOffset) * twinkleSpeed) * 0.5f + 0.5f;
            float alpha = Mathf.Lerp(minAlpha, baseAlpha, pulse);

            Color color = baseColor;
            color.a = alpha;

            if (useColorBlink)
            {
                float colorPulse = Mathf.Sin((Time.time + twinkleOffset) * colorBlinkSpeed) * 0.5f + 0.5f;
                Color blue = new Color(0.55f, 0.75f, 1f, alpha);
                Color red = new Color(1f, 0.45f, 0.45f, alpha);
                color = Color.Lerp(blue, red, colorPulse);
            }

            image.color = color;
        }

        private void RandomizeStar()
        {
            if (starSprites != null && starSprites.Length > 0)
            {
                image.sprite = starSprites[Random.Range(0, starSprites.Length)];
            }

            float size = Random.Range(minSize, maxSize);
            rectTransform.sizeDelta = new Vector2(size, size);

            float alpha = Random.Range(minAlpha, maxAlpha);
            image.color = new Color(1f, 1f, 1f, alpha);

            image.raycastTarget = false;
            image.maskable = false;
        }
    }
}