// =============================================================================
//  Factory Frenzy - Editeur de niveaux RV
//  VRGrabManager.cs                   ->  fonctionnalite VR-FP1-3
//  ---------------------------------------------------------------------------
//  "Attraper et placer des objets"
//
//  Chaque element editable porte un composant XRGrabInteractable (XR
//  Interaction Toolkit) : l'attraper se fait donc nativement, avec le rayon ou
//  la main directe des manettes.
//
//  Ce gestionnaire ajoute la logique "editeur de niveau" :
//      * il s'abonne aux elements crees (VRLevelEvents.ElementSpawned) et
//        branche automatiquement les composants XR si besoin ;
//      * il met l'element en surbrillance pendant la prise (VR-FP1-2) ;
//      * il signale la prise / le depot (OnGrabStateChanged) : VRDeleteManager
//        s'en sert pour interdire la suppression pendant une manipulation ;
//      * il place l'element au sol ou sur la grille au relachement (option) ;
//      * il demande le retour sonore + vibration (selection puis depot).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FactoryFrenzy.VR
{
    /// <summary>
    /// VR-FP1-3 : attraper et placer des objets.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRGrabManager : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Options
        // ---------------------------------------------------------------------

        [Header("Options de manipulation")]
        [Tooltip("Ajoute automatiquement Rigidbody + XRGrabInteractable si le prefab n'en a pas.")]
        public bool autoAddGrabComponents = true;

        [Tooltip("Aligner l'element sur une grille au relachement.")]
        public bool snapToGridOnRelease = false;

        [Tooltip("Pas de la grille (metres).")]
        public float gridSize = 0.25f;

        [Tooltip("Empecher l'element de tomber (les elements restent en place).")]
        public bool keepElementsFloating = true;

        [Tooltip("Logs Console des prises / depots.")]
        public bool logGrabs = false;

        // ---------------------------------------------------------------------
        // Evenements
        // ---------------------------------------------------------------------

        /// <summary>Passe a true a la prise, false au relachement.</summary>
        public event Action<bool> OnGrabStateChanged;

        /// <summary>L'utilisateur tient-il un element en ce moment ?</summary>
        public bool IsGrabbing { get; private set; }

        /// <summary>Element actuellement tenu (ou null).</summary>
        public VR_EditableElement CurrentElement { get; private set; }

        private readonly Dictionary<XRGrabInteractable, VR_EditableElement> m_Tracked =
            new Dictionary<XRGrabInteractable, VR_EditableElement>();

        // ---------------------------------------------------------------------
        // Cycle de vie Unity
        // ---------------------------------------------------------------------

        private void OnEnable()
        {
            VRLevelEvents.ElementSpawned += RegisterElement;

            // Les elements deja presents dans la scene (ex : decor editable).
            foreach (VR_EditableElement element in VR_EditableElement.All)
            {
                if (element != null) RegisterElement(element.gameObject);
            }
        }

        private void OnDisable()
        {
            VRLevelEvents.ElementSpawned -= RegisterElement;

            foreach (KeyValuePair<XRGrabInteractable, VR_EditableElement> pair in m_Tracked)
            {
                if (pair.Key == null) continue;

                pair.Key.selectEntered.RemoveListener(OnSelectEntered);
                pair.Key.selectExited.RemoveListener(OnSelectExited);
            }

            m_Tracked.Clear();
            CurrentElement = null;
            IsGrabbing = false;
        }

        // ---------------------------------------------------------------------
        // Enregistrement des elements
        // ---------------------------------------------------------------------

        /// <summary>
        /// Branche les evenements d'attrape / depot sur un element du niveau.
        /// </summary>
        public void RegisterElement(GameObject elementObject)
        {
            if (elementObject == null) return;

            VR_EditableElement element = elementObject.GetComponent<VR_EditableElement>();
            if (element == null) element = elementObject.GetComponentInChildren<VR_EditableElement>();
            if (element == null) return;

            XRGrabInteractable grab = element.GetComponent<XRGrabInteractable>();

            if (grab == null && autoAddGrabComponents)
            {
                // Les prefabs de Prefabs_VR_Editor possedent deja ces composants,
                // mais on securise le cas d'un prefab quelconque.
                Rigidbody body = element.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = element.gameObject.AddComponent<Rigidbody>();
                }

                body.isKinematic = true;
                body.useGravity = !keepElementsFloating;

                grab = element.gameObject.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;

                Debug.Log($"[VR-FP1-3] Composants d'attrape ajoutes automatiquement sur {element.name}.");
            }

            if (grab == null || m_Tracked.ContainsKey(grab)) return;

            m_Tracked.Add(grab, element);
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.selectExited.AddListener(OnSelectExited);
        }

        // ---------------------------------------------------------------------
        // Evenements XR Interaction Toolkit
        // ---------------------------------------------------------------------

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            VR_EditableElement element = GetElementFromArgs(args);
            if (element == null) return;

            CurrentElement = element;
            IsGrabbing = true;

            element.OnGrabbed();
            element.SetSelected(true);

            VRLevelEvents.RaiseFeedback(VRFeedbackType.Select);
            OnGrabStateChanged?.Invoke(true);

            if (logGrabs) Debug.Log($"[VR-FP1-3] Element attrape : {element.GetTypeName()}.");
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            VR_EditableElement element = GetElementFromArgs(args);
            if (element == null) return;

            element.OnReleased();
            element.SetSelected(false);

            if (snapToGridOnRelease)
            {
                element.transform.position = SnapToGrid(element.transform.position);
            }

            CurrentElement = null;
            IsGrabbing = false;

            VRLevelEvents.RaiseFeedback(VRFeedbackType.Place);
            OnGrabStateChanged?.Invoke(false);

            if (logGrabs) Debug.Log($"[VR-FP1-3] Element place : {element.GetTypeName()} en {element.transform.position}.");
        }

        private VR_EditableElement GetElementFromArgs(SelectEnterEventArgs args)
        {
            if (args.interactableObject == null) return null;
            return args.interactableObject.transform.GetComponentInParent<VR_EditableElement>();
        }

        private VR_EditableElement GetElementFromArgs(SelectExitEventArgs args)
        {
            if (args.interactableObject == null) return null;
            return args.interactableObject.transform.GetComponentInParent<VR_EditableElement>();
        }

        // ---------------------------------------------------------------------
        // Outils
        // ---------------------------------------------------------------------

        /// <summary>Aligne une position sur la grille de construction.</summary>
        public Vector3 SnapToGrid(Vector3 position)
        {
            if (gridSize <= 0f) return position;

            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                position.y,
                Mathf.Round(position.z / gridSize) * gridSize);
        }

        /// <summary>Desenregistre un element (appele par VRDeleteManager).</summary>
        public void UnregisterElement(GameObject elementObject)
        {
            if (elementObject == null) return;

            XRGrabInteractable grab = elementObject.GetComponent<XRGrabInteractable>();
            if (grab == null || !m_Tracked.ContainsKey(grab)) return;

            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
            m_Tracked.Remove(grab);
        }
    }
}
