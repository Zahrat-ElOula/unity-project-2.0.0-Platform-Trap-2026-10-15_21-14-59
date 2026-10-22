using UnityEngine;
using UnityEngine.UI;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Fond dégradé du menu principal (style usine futuriste).
    /// Génère une texture dégradée verticale entre deux couleurs et l'applique
    /// à l'Image du GameObject — sans asset externe (donc reproductible au build).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class GradientBackground : MonoBehaviour
    {
        [Tooltip("Couleur en haut de l'écran (bleu nuit #1A1A2E).")]
        [SerializeField] private Color topColor = new Color32(0x1A, 0x1A, 0x2E, 0xFF);

        [Tooltip("Couleur en bas de l'écran (bleu profond #16213E).")]
        [SerializeField] private Color bottomColor = new Color32(0x16, 0x21, 0x3E, 0xFF);

        [Tooltip("Hauteur de la texture générée (pixels). Plus grand = dégradé plus fin.")]
        [SerializeField] private int textureHeight = 256;

        private void Start()
        {
            ApplyGradient();
        }

        /// <summary>Génère et applique la texture de dégradé sur l'Image.</summary>
        private void ApplyGradient()
        {
            Texture2D texture = new Texture2D(1, textureHeight);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < textureHeight; y++)
            {
                // y = 0 en bas -> couleur du bas ; y = max -> couleur du haut.
                float t = (float)y / (textureHeight - 1);
                Color color = Color.Lerp(bottomColor, topColor, t);
                texture.SetPixel(0, y, color);
            }
            texture.Apply();

            Image image = GetComponent<Image>();
            image.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, textureHeight),
                new Vector2(0.5f, 0.5f));
            image.color = Color.white; // la couleur vient de la texture
        }
    }
}
