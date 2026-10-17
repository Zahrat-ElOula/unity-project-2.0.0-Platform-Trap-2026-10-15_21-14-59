using System;
using UnityEngine;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// Types de retours sensoriels demandés par les autres scripts VR.
    /// </summary>
    public enum VRFeedbackType
    {
        Select,
        Place,
        Delete,
        Save,
        Error
    }

    /// <summary>
    /// Bus d'événements centralisé pour la partie VR.
    /// </summary>
    public static class VRLevelEvents
    {
        // -----------------------------------------------------------------
        // Événements
        // -----------------------------------------------------------------

        /// <summary>Demande un retour sensoriel (son + vibration).</summary>
        public static event Action<VRFeedbackType> FeedbackRequested;

        /// <summary>Notifie qu'un élément a été créé (spawn).</summary>
        public static event Action<GameObject> ElementSpawned;

        /// <summary>Notifie qu'un élément a été supprimé.</summary>
        public static event Action<GameObject> ElementDeleted;

        /// <summary>Notifie que le niveau a été exporté.</summary>
        public static event Action<string> LevelExported;

        // -----------------------------------------------------------------
        // Méthodes d'invocation (compatibilité totale avec les scripts générés)
        // -----------------------------------------------------------------

        /// <summary>Demande un retour sensoriel. (utilisé par VRDeleteManager, VRUIManager, etc.)</summary>
        public static void RaiseFeedback(VRFeedbackType type)
        {
            FeedbackRequested?.Invoke(type);
        }

        /// <summary>Notifie qu'un élément a été créé. (utilisé par VRUIManager)</summary>
        public static void RaiseElementSpawned(GameObject element)
        {
            ElementSpawned?.Invoke(element);
        }

        /// <summary>Notifie qu'un élément a été supprimé.</summary>
        public static void RaiseElementDeleted(GameObject element)
        {
            ElementDeleted?.Invoke(element);
        }

        /// <summary>Notifie que le niveau a été exporté.</summary>
        public static void RaiseLevelExported(string path)
        {
            LevelExported?.Invoke(path);
        }

        // -----------------------------------------------------------------
        // Alias (pour compatibilité avec d'autres scripts éventuels)
        // -----------------------------------------------------------------

        public static void RequestFeedback(VRFeedbackType type) => RaiseFeedback(type);
        public static void NotifyElementCreated(GameObject element) => RaiseElementSpawned(element);
        public static void NotifyElementDeleted(GameObject element) => RaiseElementDeleted(element);
        public static void NotifyLevelExported(string path) => RaiseLevelExported(path);
    }
}