using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class ModelLibrary : MonoBehaviour
{
    [SerializeField] private List<GameObject> models;

    #region Singleton
    public static ModelLibrary Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate ModelLibrary found on {gameObject.name}. Destroying self.");
            Destroy(gameObject); // Destroy this duplicate instance
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    public GameObject GetModelByID(int modelID)
    {
        if (modelID >= 0 && modelID < models.Count)
        {
            return models[modelID];
        }
        else
        {
            Debug.LogWarning($"ModelLibrary: Requested model ID {modelID} is out of bounds (Library size: {models.Count}).");
            return null;
        }
    }
}