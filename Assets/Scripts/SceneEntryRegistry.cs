using System.Collections.Generic;
using UnityEngine;

public class SceneEntryRegistry : MonoBehaviour, IGameService
{
    public static SceneEntryRegistry Instance;

    private Dictionary<string, IPassage> points = new();

    public void Register(IPassage point)
    {
        if (!points.ContainsKey(point.EntryName))
            points.Add(point.EntryName, point);
        else
            Debug.LogWarning($"Duplicate entry name: {point.EntryName}");
    }
    public void ClearPassages()
    {
        points.Clear();
    }
    public IPassage Get(string name)
    {
        points.TryGetValue(name, out var point);
        return point;
    }

    public void Init()
    {
        if(Instance == null) 
            Instance = this;
    }

    private void OnDestroy()
    {
        Instance = null;
    }
}

public interface IPassage
{
    public string EntryName { get; }

    public Transform SpawnPos {  get; }
}