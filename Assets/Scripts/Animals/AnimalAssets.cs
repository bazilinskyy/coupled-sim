using System;
using UnityEngine;

/// <summary>The animal prefabs (Resources/AnimalAssets), built by AnimalPrefabBuilder.</summary>
[CreateAssetMenu(menuName = "Coupled Sim/Animal assets")]
public class AnimalAssets : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public AnimalSpecies Species;
        public GameObject Prefab;
    }

    public Entry[] Animals;

    public GameObject Get(AnimalSpecies species)
    {
        if (Animals == null) return null;
        foreach (var e in Animals) if (e.Species == species) return e.Prefab;
        return null;
    }
}
