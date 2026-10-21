using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VRKeyboardBuilder : MonoBehaviour
{
    [Header("Références")]
    public Transform keyboardParent; // Le Canvas VR_Keyboard
    public VRKeyboardSimple keyboardLogic; // Le script qui gère la saisie

    [Header("Style")]
    public float buttonWidth = 60f;
    public float buttonHeight = 60f;
    public float spacing = 5f;

    void Start()
    {
        BuildKeyboard();
    }

    public void BuildKeyboard()
    {
        if (keyboardParent == null || keyboardLogic == null) return;

        // On ajoute les chiffres et quelques caractères spéciaux à la chaîne
        string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.";

        for (int i = 0; i < characters.Length; i++)
        {
            CreateKey(characters[i].ToString(), characters[i].ToString());
        }

        // Bouton DEL
        CreateKey("DEL", "DEL", () => keyboardLogic.Backspace(), new Color(0.8f, 0.3f, 0.3f));

        // Bouton OK
        CreateKey("OK", "OK", () => keyboardLogic.Confirm(), new Color(0.2f, 0.6f, 0.2f));
    }

    private void CreateKey(string label, string value, System.Action action = null, Color? color = null)
    {
        // Création du GameObject
        GameObject go = new GameObject($"Key_{label}", typeof(RectTransform));
        go.transform.SetParent(keyboardParent, false);

        // Image de fond
        Image img = go.AddComponent<Image>();
        img.color = color ?? new Color(0.2f, 0.2f, 0.2f, 0.9f);

        // Bouton
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Texte
        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Action du bouton
        if (action != null)
            btn.onClick.AddListener(() => action());
        else
            btn.onClick.AddListener(() => keyboardLogic.AppendCharacter(value));

        // Taille (LayoutElement)
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = buttonWidth;
        le.preferredHeight = buttonHeight;
    }

}