// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VRTeleportationManager.cs          ->  fonctionnalite VR-FP1-1
//  ---------------------------------------------------------------------------
//  "Teleportation de l'avatar RV"
//
//  Le deplacement en VR est assure par le composant TeleportationProvider de
//  l'XR Origin (XR Interaction Toolkit) et par une zone teleportable
//  (TeleportationArea) posee sur le sol de la DemoScene_Forest.
//
//  Ce gestionnaire sert de point d'entree unique et documente :
//      * il retrouve automatiquement le TeleportationProvider et la
//        TeleportationArea de la scene (aucune reference a casser) ;
//      * il expose Teleport(...) pour teleporter l'avatar par script
//        (utile pour un bouton de retour au point de depart) ;
//      * il permet d'activer / desactiver la zone teleportable, par exemple
//        de la couper pendant une manipulation precise.
//
//  Rappel d'utilisation en jeu (Quest + Link) :
//      * pousser le joystick vers l'avant (main gauche ou droite) fait
//        apparaitre l'arc de teleportation ;
//      * viser une surface de la zone teleportable ;
//      * relacher le joystick pour se teleporter.
// =============================================================================

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// VR-FP1-1 : teleportation de l'avatar RV.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRTeleportationManager : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // References (retrouvees automatiquement si elles sont vides)
        // ---------------------------------------------------------------------

        [Header("References (auto-detectees si vides)")]
        [Tooltip("Composant TeleportationProvider porte par l'XR Origin.")]
        public TeleportationProvider teleportationProvider;

        [Tooltip("Zone du sol sur laquelle l'avatar peut se teleporter.")]
        public TeleportationArea teleportationArea;

        [Tooltip("Point de depart de l'avatar (bouton 'retour au depart').")]
        public Transform spawnPoint;

        [Header("Options")]
        [Tooltip("Ecrire un message dans la Console a chaque teleportation.")]
        public bool logTeleports = true;

        /// <summary>Nombre de teleportations effectuees via ce gestionnaire.</summary>
        public int TeleportCount { get; private set; }

        // ---------------------------------------------------------------------
        // Cycle de vie Unity
        // ---------------------------------------------------------------------

        private void Awake()
        {
            EnsureReferences();
        }

        // ---------------------------------------------------------------------
        // API publique
        // ---------------------------------------------------------------------

        /// <summary>
        /// Retrouve le TeleportationProvider et la TeleportationArea de la scene
        /// si les references n'ont pas ete renseignees dans l'Inspector.
        /// </summary>
        public void EnsureReferences()
        {
            if (teleportationProvider == null)
            {
                teleportationProvider = FindObjectOfType<TeleportationProvider>();
            }

            if (teleportationArea == null)
            {
                teleportationArea = FindObjectOfType<TeleportationArea>();
            }

            if (teleportationProvider == null)
            {
                Debug.LogWarning("[VR-FP1-1] Aucun TeleportationProvider dans la scene : " +
                                 "verifier que l'XR Origin (XR Rig) est bien present.");
            }

            if (teleportationArea == null)
            {
                Debug.LogWarning("[VR-FP1-1] Aucune TeleportationArea dans la scene : " +
                                 "verifier que le sol possede bien un composant TeleportationArea.");
            }
        }

        /// <summary>La teleportation est-elle utilisable ?</summary>
        public bool IsTeleportAvailable => teleportationProvider != null && teleportationArea != null;

        /// <summary>
        /// Demande la teleportation de l'avatar a une position / orientation donnee.
        /// </summary>
        public bool Teleport(Vector3 destinationPosition, Quaternion destinationRotation)
        {
            if (teleportationProvider == null)
            {
                EnsureReferences();
                if (teleportationProvider == null) return false;
            }

            TeleportRequest request = new TeleportRequest
            {
                destinationPosition = destinationPosition,
                destinationRotation = destinationRotation,
                matchOrientation = MatchOrientation.WorldSpaceUp
            };

            bool queued = teleportationProvider.QueueTeleportRequest(request);

            if (queued)
            {
                TeleportCount++;

                if (logTeleports)
                {
                    Debug.Log($"[VR-FP1-1] Teleportation vers {destinationPosition} (total : {TeleportCount}).");
                }
            }
            else
            {
                Debug.LogWarning("[VR-FP1-1] Teleportation refusee par le TeleportationProvider.");
            }

            return queued;
        }

        /// <summary>Teleporte l'avatar sur un Transform cible (position + orientation).</summary>
        public bool TeleportTo(Transform target)
        {
            if (target == null) return false;
            return Teleport(target.position, target.rotation);
        }

        /// <summary>Retour au point de depart de l'avatar (bouton de secours).</summary>
        public bool TeleportToSpawnPoint()
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning("[VR-FP1-1] Aucun spawnPoint renseigne.");
                return false;
            }

            return TeleportTo(spawnPoint);
        }

        /// <summary>Active ou desactive la zone teleportable du sol.</summary>
        public void SetTeleportationAreaEnabled(bool value)
        {
            if (teleportationArea != null)
            {
                teleportationArea.enabled = value;
            }
        }
    }
}
