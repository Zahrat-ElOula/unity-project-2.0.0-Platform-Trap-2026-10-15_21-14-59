using System;
using System.Collections.Generic;
using UnityEngine;

namespace FactoryFrenzy.Data
{
    /// <summary>
    /// Racine du contrat JSON échangé entre la partie VR (export) et la partie PC (import).
    /// Les noms des champs DOIVENT rester identiques à ceux du JSON
    /// (JsonUtility fait la correspondance par nom, sensible à la casse).
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public string levelName;
        public List<ElementData> elements;
    }

    /// <summary>
    /// Un élément de niveau : une plateforme, un piège, etc.
    /// 'moveParams' n'est renseigné (non nul) que pour les plateformes mouvantes.
    /// </summary>
    [Serializable]
    public class ElementData
    {
        public string type;
        public Vector3 position;
        public Vector3 rotation;   // Euler (degrés), tel que fourni par la VR
        public Vector3 scale;
        public MoveParams moveParams;
    }

    /// <summary>
    /// Paramètres de déplacement d'une plateforme mouvante :
    /// départ, arrivée, durée du trajet (secondes).
    /// </summary>
    [Serializable]
    public class MoveParams
    {
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float duration;
    }
}
