using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Fait "flotter" doucement un élément UI (le titre) : léger mouvement
    /// sinusoïdal vertical. Purement cosmétique.
    /// </summary>
    public class TitleFloat : MonoBehaviour
    {
        [Tooltip("Amplitude verticale du flottement (pixels, en unités locales UI).")]
        [SerializeField] private float amplitude = 12f;

        [Tooltip("Vitesse du flottement (cycles/seconde).")]
        [SerializeField] private float speed = 1f;

        private RectTransform rect;
        private Vector2 basePosition;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            basePosition = rect.anchoredPosition;
        }

        private void Update()
        {
            // Décalage vertical sinus : -amplitude .. +amplitude.
            float offset = Mathf.Sin(Time.time * speed * Mathf.PI * 2f) * amplitude;
            rect.anchoredPosition = basePosition + new Vector2(0f, offset);
        }
    }
}
