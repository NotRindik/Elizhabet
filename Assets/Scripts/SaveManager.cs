using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;


public interface ISaveModule
{
    string Key { get; }
    object Capture();
    void Restore(object data);
}

public class WorldObjectsStateSave : ISaveModule
{
    public string Key => "worldState";

    public object Capture()
    {
        throw new System.NotImplementedException();
    }

    public void Restore(object data)
    {
        throw new System.NotImplementedException();
    }
}
public class SaveManager
{
    List<ISaveModule> modules;
    static readonly string Path =
    Application.streamingAssetsPath + "/save.json";
    public int CurrSlot;

    public void Save()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            var json = JsonConvert.SerializeObject(modules[i], Formatting.Indented);
            File.WriteAllText(Path, json);
        }
    }

    public void Load()
    {
        if (!File.Exists(Path))
            return;

        var json = File.ReadAllText(Path);
        JsonConvert.DeserializeObject(json);
    }
}
