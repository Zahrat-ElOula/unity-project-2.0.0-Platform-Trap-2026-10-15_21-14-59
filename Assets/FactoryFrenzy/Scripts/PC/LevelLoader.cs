using System.Collections.Generic;
using System.IO;
using FactoryFrenzy.Data;
using UnityEngine;

namespace FactoryFrenzy.PC
{
    /// <summary>
    /// Importe un niveau exporté par la partie VR (fichier JSON dans StreamingAssets)
    /// et le reconstruit dans la scène PC.
    ///
    /// Les prefabs assignés dans l'Inspector sont des prefabs FONCTIONNELS : ils portent
    /// déjà leur script (MovingPlatform, BumperTrap, FanTrap, ShooterTrap, Trampoline,
    /// FinishPlatform...). Le loader se contente donc d'INSTANCIER, puis de CONFIGURER
    /// les cas particuliers (plateforme mouvante : arrivée + durée).
    ///
    /// Mapping 'type' JSON -> prefab : voir GetPrefabByType.
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        [Header("Prefabs (à assigner dans l'Inspector)")]
        [SerializeField] private GameObject startPrefab;
        [SerializeField] private GameObject endPrefab;
        [SerializeField] private GameObject classicalPrefab;
        [SerializeField] private GameObject movePrefab;
        [SerializeField] private GameObject checkpointPrefab;
        [SerializeField] private GameObject bumperPrefab;
        [SerializeField] private GameObject fanPrefab;
        [SerializeField] private GameObject shooterPrefab;
        [SerializeField] private GameObject trampolinePrefab;

        [Header("Réglages")]
        [Tooltip("Décalage appliqué à toutes les positions importées (monde).")]
        [SerializeField] private Vector3 levelOffset = Vector3.zero;

        [Tooltip("Parent des objets instanciés (vide = racine de scène).")]
        [SerializeField] private Transform spawnedParent;

        [Tooltip("Charge automatiquement le niveau au démarrage.")]
        [SerializeField] private bool loadOnStart = true;

        [Tooltip("Nom du fichier JSON dans StreamingAssets.")]
        [SerializeField] private string defaultFileName = "DemoScene_Forest.json";

        [Header("Anti-doublon (Phase 5)")]
        [Tooltip("Si vrai, désactive d'abord les objets manuels listés ci-dessous.")]
        [SerializeField] private bool clearExistingOnLoad = false;

        [Tooltip("Objets de niveau placés à la main, à désactiver avant l'import.")]
        [SerializeField] private List<GameObject> existingToClear = new List<GameObject>();

        private void Start()
        {
            if (loadOnStart) LoadLevel(defaultFileName);
        }

        /// <summary>
        /// Lit le JSON, le désérialise et instancie chaque élément.
        /// </summary>
        public void LoadLevel(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName);

            if (!File.Exists(path))
            {
                Debug.LogError("[LevelLoader] Fichier introuvable : " + path);
                return;
            }

            string json = File.ReadAllText(path);
            LevelData data = JsonUtility.FromJson<LevelData>(json);

            if (data == null || data.elements == null)
            {
                Debug.LogError("[LevelLoader] JSON illisible ou sans 'elements' : " + path);
                return;
            }

            Debug.Log("[LevelLoader] Niveau '" + data.levelName + "' : " + data.elements.Count + " élément(s) à instancier.");

            if (clearExistingOnLoad)
            {
                foreach (GameObject go in existingToClear)
                {
                    if (go != null) go.SetActive(false);
                }
            }

            foreach (ElementData el in data.elements)
            {
                SpawnElement(el);
            }
        }

        /// <summary>
        /// Instancie un élément : choisit le prefab, le place, puis le configure.
        /// </summary>
        private void SpawnElement(ElementData el)
        {
            GameObject prefab = GetPrefabByType(el.type);
            if (prefab == null) return;

            Vector3 position = el.position + levelOffset;
            Quaternion rotation = Quaternion.Euler(el.rotation);

            GameObject go = Instantiate(prefab, position, rotation, spawnedParent);
            go.name = prefab.name + " [" + el.type + "]";
            go.transform.localScale = el.scale;

            ConfigureElement(go, el);

            Debug.Log("[LevelLoader] " + el.type + " instancié à " + position, go);
        }

        /// <summary>
        /// Table de correspondance : type JSON -> prefab (prefabs fonctionnels + visuels).
        /// </summary>
        public GameObject GetPrefabByType(string type)
        {
            switch (type)
            {
                case "Platform_Start": return startPrefab;
                case "Platform_End": return endPrefab;
                case "Platform_Classical": return classicalPrefab;
                case "Platform_Move": return movePrefab;
                case "Platform_Checkpoint": return checkpointPrefab;
                case "Trap_Bumper": return bumperPrefab;
                case "Trap_Fan": return fanPrefab;
                case "Trap_Shooter": return shooterPrefab;
                case "Trap_Trampoline": return trampolinePrefab;
                default:
                    Debug.LogWarning("[LevelLoader] Type inconnu : " + type);
                    return null;
            }
        }

        /// <summary>
        /// Configure les cas particuliers après instanciation.
        /// Les prefabs fonctionnels portent déjà leur script : rien à ajouter.
        /// Seule la plateforme mouvante a besoin de connaître son arrivée + sa durée.
        /// </summary>
        private void ConfigureElement(GameObject go, ElementData el)
        {
            if (el.type == "Platform_Move")
            {
                MovingPlatform mp = go.GetComponent<MovingPlatform>();
                if (mp != null && el.moveParams != null)
                {
                    // La position initiale de l'instance fait office de 'startPosition'.
                    mp.Configure(el.moveParams.endPosition + levelOffset, el.moveParams.duration);
                }
                else
                {
                    Debug.LogWarning("[LevelLoader] MovingPlatform manquant sur le prefab de mouvement.", go);
                }
            }
        }
    }
}
