using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;


public interface ISaveModule
{
    string Key { get; }
    System.Type DataType { get; }

    object CaptureBoxed();
    void RestoreBoxed(object data);
}

public interface ISaveModule<T> : ISaveModule
{
    new T Capture();
    void Restore(T data);
}

public abstract class SaveModule<T> : ISaveModule<T>
{
    public abstract string Key { get; }

    public System.Type DataType { get => GetType(); }

    public abstract T Capture();
    public abstract void Restore(T data);

    object ISaveModule.CaptureBoxed()
        => Capture();   

    void ISaveModule.RestoreBoxed(object data)
        => Restore((T)data);
}

public class GlobalSaves : SaveModule<Dictionary<string, string>>
{
    public Dictionary<string, string> worldFlags = new Dictionary<string, string>();

    public override string Key => "GLOBALDATA";

    public void SetData(string key, string value)
    {
        worldFlags[key] = value;
    }
    public bool Exist(string key)
    {
        return worldFlags.ContainsKey(key);
    }
    public string GetData(string key)
    {
        return worldFlags[key];
    }

    public override Dictionary<string, string> Capture()
    {
        return worldFlags;
    }

    public override void Restore(Dictionary<string, string> data)
    {
        worldFlags = data;
    }
}


public class WorldObjectsStateSave : SaveModule<Dictionary<string, string>>
{

    private Dictionary<string,string> worldFlags = new Dictionary<string, string>();

    public override string Key => "WorldState";

    public void SetData(string key,string value)
    {
        worldFlags[key] = value;
    }
    public bool Exist(string key)
    {
        return worldFlags.ContainsKey(key);  
    }
    public string GetData(string key)
    {
        return worldFlags[key];
    }

    public override Dictionary<string, string> Capture()
    {
        return worldFlags;
    }

    public override void Restore(Dictionary<string, string> data)
    {
        worldFlags = data;
    }
}
public static class WorldKeyBuilder
{
    public static string Build(Component c, string localKey)
    {
        return $"{SceneManager.GetActiveScene().name}/" +
               $"{c.gameObject.name}/" +
               $"{localKey}";
    }
}
public class SaveManager
{
    static SaveManager _instance;
    public static SaveManager Instance =>
        _instance ??= new SaveManager();

    private ISaveModule[] _modules;
    public ISaveModule[] Modules { get=>_modules; 
        set 
        {
            modules?.Clear();
            _modules = value;
            for (int i = 0; i < _modules.Length; i++)
            {
                modules?.Add(_modules[i].DataType, _modules[i]);
            }
        } }

    public Dictionary<Type, ISaveModule> modules { get; private set; } = new();

    static readonly string BasePath =
        Application.persistentDataPath + "/saves/";

    public int CurrSlot;

    private SaveManager()
    {
        Directory.CreateDirectory(BasePath);
    }

    public void Save()
    {
        var slotPath = $"{BasePath}slot_{CurrSlot}/";
        if(!Directory.Exists(slotPath)) 
            Directory.CreateDirectory(slotPath);

        foreach (var m in _modules)
        {
            var json = JsonConvert.SerializeObject(
                m.CaptureBoxed(),
                Formatting.Indented
            );

            File.WriteAllText(
                $"{slotPath}{m.Key}.json",
                json
            );
        }
    }

    public void Load()
    {
        var slotPath = $"{BasePath}slot_{CurrSlot}/";

        foreach (var m in _modules)
        {
            var file = $"{slotPath}{m.Key}.json";
            if (!File.Exists(file))
                continue;

            var json = File.ReadAllText(file);
            var data = JsonConvert.DeserializeObject(
                json,
                m.DataType
            );

            m.RestoreBoxed(data);
        }
    }
}
