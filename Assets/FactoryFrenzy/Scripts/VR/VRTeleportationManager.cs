using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRTeleportationManager : MonoBehaviour
{
    [Header("Références (auto-détectées si vides)")]
    public TeleportationProvider teleportationProvider;
    public TeleportationArea teleportationArea;
    public Transform spawnPoint;

    [Header("Options")]
    public bool logTeleports = true;

    void Start()
    {
        // Auto-détection si les champs sont vides
        if (teleportationProvider == null)
            teleportationProvider = GetComponent<TeleportationProvider>();

        if (teleportationArea == null)
            teleportationArea = FindObjectOfType<TeleportationArea>();

        if (teleportationProvider == null)
            Debug.LogWarning("[VRTeleportationManager] teleportationProvider non assigné !");
        if (teleportationArea == null)
            Debug.LogWarning("[VRTeleportationManager] teleportationArea non assigné !");
    }

    void Update()
    {
        // Le Teleportation Provider gère automatiquement la téléportation
        // via le XR Ray Interactor du contrôleur.
    }

    public void OnTeleport()
    {
        if (logTeleports)
            Debug.Log("[VRTeleportationManager] Téléportation effectuée.");
    }
}