using UnityEngine;

public class VRKeyboardPositioner : MonoBehaviour
{
    public Transform headCamera;
    public float distance = 0.8f;
    public float verticalOffset = -0.2f; // Ajuste pour monter/descendre

    void OnEnable()
    {
        if (headCamera == null) return;

        // Place le clavier devant la tête
        Vector3 forward = headCamera.forward;
        forward.y = 0;
        forward.Normalize();

        transform.position = headCamera.position + forward * distance + Vector3.up * verticalOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}