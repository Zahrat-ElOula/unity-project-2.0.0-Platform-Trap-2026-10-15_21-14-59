using UnityEngine;
using UnityEngine.UI;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Halo lumineux (glow) derrière le titre : un disque jaune doux, semi-transparent,
    /// qui pulse légèrement. La texture radiale est générée par script (aucun asset).
    ///
    /// À placer sur une Image située DERRIÈRE le titre (premier enfant d'un conteneur,
    /// ou juste avant 'Title' dans le MenuUI).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TitleGlow : MonoBehaviour
    {
        [Tooltip("Couleur du halo (jaune du titre par défaut).")]
        [SerializeField] private Color glowColor = new Color(1f, 0.84f, 0f, 1f);

        [Tooltip("Opacité maximale au centre du halo (0..1).")]
        [SerializeField][Range(0f, 1f)] private float maxAlpha = 0.5f;

        [Tooltip("Vitesse de pulsation (cycles/seconde). 0 = halo fixe.")]
        [SerializeField] private float pulseSpeed = 0.6f;

        [Tooltip("Amplitude de la pulsation (0..1).")]
        [SerializeField][Range(0f, 1f)] private float pulseAmount = 0.25f;

        [Tooltip("Résolution de la texture radiale (pixels).")]
        [SerializeField] private int textureSize = 256;

        private Image image;

        private void Start()
        {
            image = GetComponent<Image>();
            image.sprite = BuildRadialSprite();
            image.color = new Color(glowColor.r, glowColor.g, glowColor.b, maxAlpha);
        }

        private void Update()
        {
            if (image == null || pulseSpeed <= 0f) return;

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
            float alpha = Mathf.Clamp01(maxAlpha * pulse);
            image.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
        }

        /// <summary>Génère un disque à dégradé radial (opaque au centre, transparent aux bords).</summary>
        private Sprite BuildRadialSprite()
        {
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (textureSize - 1) * 0.5f;
            float radius = center;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / radius;
                    // 1 au centre -> 0 au bord, avec une décroissance douce.
                    float falloff = Mathf.Clamp01(1f - distance);
                    falloff = falloff * falloff; // dégradé plus "doux"
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, falloff));
                }
            }
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, textureSize, textureSize),
                new Vector2(0.5f, 0.5f));
        }
    }
}
