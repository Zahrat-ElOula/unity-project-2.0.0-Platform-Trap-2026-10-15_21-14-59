// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VRFeedbackManager.cs               ->  fonctionnalite VR-FP1-6
//  ---------------------------------------------------------------------------
//  "Son et vibration"
//
//  Ce gestionnaire est le seul responsable du retour sensoriel :
//      * il s'abonne au bus d'evenements VRLevelEvents.FeedbackRequested ;
//      * selon le type demande (Select / Place / Delete / Save / Error) il
//        joue un son et envoie une impulsion haptique aux manettes ;
//      * il fournit aussi des methodes publiques (PlaySelectFeedback(), ...)
//        utilisables directement depuis l'Inspector (bouton, UnityEvent).
//
//  Aucun fichier audio n'est fourni par le projet : si les AudioClip ne sont
//  pas renseignes, le script genere des "bips" par code (GenerateDefaultClips).
//  Il suffit de glisser des AudioClip dans les champs pour les remplacer.
//
//  Les amplitudes / durees des vibrations sont reglables dans l'Inspector.
// =============================================================================

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// VR-FP1-6 : retour sonore et vibration.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRFeedbackManager : MonoBehaviour
    {
        /// <summary>Instance unique, pratique pour les autres scripts / UnityEvents.</summary>
        public static VRFeedbackManager Instance { get; private set; }

        // ---------------------------------------------------------------------
        // Audio
        // ---------------------------------------------------------------------

        [Header("Audio")]
        [Tooltip("Source audio utilisee pour les retours. Creee automatiquement si vide.")]
        public AudioSource audioSource;

        public AudioClip selectClip;
        public AudioClip placeClip;
        public AudioClip deleteClip;
        public AudioClip saveClip;
        public AudioClip errorClip;

        [Tooltip("Generer des bips par code lorsqu'aucun AudioClip n'est fourni.")]
        public bool generateDefaultClips = true;

        [Range(0f, 1f)] public float volume = 0.6f;

        // ---------------------------------------------------------------------
        // Vibration
        // ---------------------------------------------------------------------

        [Header("Vibration (haptique)")]
        [Tooltip("Manette gauche (ActionBasedController ou XRController).")]
        public XRBaseController leftController;

        [Tooltip("Manette droite (ActionBasedController ou XRController).")]
        public XRBaseController rightController;

        [Tooltip("Vibrer sur les deux manettes (sinon une seule).")]
        public bool vibrateBothControllers = true;

        [Header("Amplitudes / durees des vibrations")]
        [Range(0f, 1f)] public float selectAmplitude = 0.15f;
        public float selectDuration = 0.05f;

        [Range(0f, 1f)] public float placeAmplitude = 0.25f;
        public float placeDuration = 0.08f;

        [Range(0f, 1f)] public float deleteAmplitude = 0.40f;
        public float deleteDuration = 0.12f;

        [Range(0f, 1f)] public float saveAmplitude = 0.35f;
        public float saveDuration = 0.18f;

        [Range(0f, 1f)] public float errorAmplitude = 0.60f;
        public float errorDuration = 0.20f;

        private AudioClip m_GeneratedSelect;
        private AudioClip m_GeneratedPlace;
        private AudioClip m_GeneratedDelete;
        private AudioClip m_GeneratedSave;
        private AudioClip m_GeneratedError;

        // ---------------------------------------------------------------------
        // Cycle de vie Unity
        // ---------------------------------------------------------------------

        private void Awake()
        {
            Instance = this;
            EnsureAudio();
            EnsureControllers();
        }

        private void OnEnable()
        {
            VRLevelEvents.FeedbackRequested += PlayFeedback;
        }

        private void OnDisable()
        {
            VRLevelEvents.FeedbackRequested -= PlayFeedback;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------------
        // Reponses au bus d'evenements
        // ---------------------------------------------------------------------

        /// <summary>Joue le retour correspondant au type demande.</summary>
        public void PlayFeedback(VRFeedbackType type)
        {
            switch (type)
            {
                case VRFeedbackType.Select: PlaySelectFeedback(); break;
                case VRFeedbackType.Place: PlayPlaceFeedback(); break;
                case VRFeedbackType.Delete: PlayDeleteFeedback(); break;
                case VRFeedbackType.Save: PlaySaveFeedback(); break;
                case VRFeedbackType.Error: PlayErrorFeedback(); break;
            }
        }

        // ---------------------------------------------------------------------
        // API publique
        // ---------------------------------------------------------------------

        public void PlaySelectFeedback()
        {
            PlayClip(selectClip != null ? selectClip : m_GeneratedSelect);
            SendHaptics(selectAmplitude, selectDuration);
        }

        public void PlayPlaceFeedback()
        {
            PlayClip(placeClip != null ? placeClip : m_GeneratedPlace);
            SendHaptics(placeAmplitude, placeDuration);
        }

        public void PlayDeleteFeedback()
        {
            PlayClip(deleteClip != null ? deleteClip : m_GeneratedDelete);
            SendHaptics(deleteAmplitude, deleteDuration);
        }

        public void PlaySaveFeedback()
        {
            PlayClip(saveClip != null ? saveClip : m_GeneratedSave);
            SendHaptics(saveAmplitude, saveDuration);
        }

        public void PlayErrorFeedback()
        {
            PlayClip(errorClip != null ? errorClip : m_GeneratedError);
            SendHaptics(errorAmplitude, errorDuration);
        }

        // ---------------------------------------------------------------------
        // Audio
        // ---------------------------------------------------------------------

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D : le retour doit toujours s'entendre
            audioSource.volume = volume;

            if (!generateDefaultClips) return;

            // Bips generes par code : aucune ressource audio n'est necessaire
            // pour demontrer la fonctionnalite "son et vibration".
            m_GeneratedSelect = CreateTone("FF_Select", 880f, 0.08f, 0.35f);
            m_GeneratedPlace = CreateTone("FF_Place", 620f, 0.10f, 0.35f);
            m_GeneratedDelete = CreateTone("FF_Delete", 320f, 0.16f, 0.40f);
            m_GeneratedSave = CreateTone("FF_Save", 1180f, 0.18f, 0.35f);
            m_GeneratedError = CreateTone("FF_Error", 180f, 0.28f, 0.45f);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;

            audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Fabrique un court bip sinusoidal avec enveloppe (attaque + decroissance)
        /// pour eviter les "clics" audio.
        /// </summary>
        private static AudioClip CreateTone(string clipName, float frequency, float duration, float amplitude)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float attack = 1f - Mathf.Exp(-time * 400f);      // montee rapide
                float decay = Mathf.Exp(-time * 14f);              // extinction douce
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * time);
                samples[i] = wave * attack * decay * amplitude;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ---------------------------------------------------------------------
        // Vibration
        // ---------------------------------------------------------------------

        private void EnsureControllers()
        {
            XRBaseController[] controllers = FindObjectsOfType<XRBaseController>();

            foreach (XRBaseController controller in controllers)
            {
                string path = GetHierarchyPath(controller.transform).ToLowerInvariant();

                if (leftController == null && path.Contains("left"))
                {
                    leftController = controller;
                    continue;
                }

                if (rightController == null && path.Contains("right"))
                {
                    rightController = controller;
                }
            }

            // Repli : on prend les deux premieres manettes trouvees.
            if (leftController == null && controllers.Length > 0) leftController = controllers[0];
            if (rightController == null && controllers.Length > 1) rightController = controllers[1];

            if (leftController == null && rightController == null)
            {
                Debug.LogWarning("[VR-FP1-6] Aucune manette (XRBaseController) trouvee : " +
                                 "seul le retour sonore sera joue.");
            }
        }

        private void SendHaptics(float amplitude, float duration)
        {
            if (duration <= 0f) return;

            if (vibrateBothControllers)
            {
                SendHapticImpulse(rightController, amplitude, duration);
                SendHapticImpulse(leftController, amplitude, duration);
            }
            else
            {
                SendHapticImpulse(rightController != null ? rightController : leftController, amplitude, duration);
            }
        }

        private static void SendHapticImpulse(XRBaseController controller, float amplitude, float duration)
        {
            if (controller == null) return;

            controller.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;

            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
