using UnityEngine;
using TMPro;

public class VRKeyboardSimple : MonoBehaviour
{
    public TMP_InputField targetInputField;

    // Appelé par les boutons A, B, C...
    public void AppendCharacter(string character)
    {
        if (targetInputField != null)
            targetInputField.text += character;
    }

    // Appelé par le bouton DEL
    public void Backspace()
    {
        if (targetInputField != null && targetInputField.text.Length > 0)
            targetInputField.text = targetInputField.text.Substring(0, targetInputField.text.Length - 1);
    }

    // Appelé par le bouton OK
    public void Confirm()
    {
        gameObject.SetActive(false);
    }
}