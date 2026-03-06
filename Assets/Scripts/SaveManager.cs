using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using DG.Tweening.Plugins.Core.PathCore;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;


public interface KVPSaves
{
    public bool Exist(string key);
}

public interface ISaveModule
{
    string Key { get; }
    System.Type DataType { get; }

    public void Save(string slotPath);
    public void Load(string slotPath);
    public void Reset(string slotPath);
}
public abstract class JsonSaveModule : ISaveModule
{
    public abstract string Key { get; }

    public Type DataType => GetType();

    public abstract void Load(string path);
    public abstract void Save(string path);

    public T Deserialize<T>(string path)
    {
        var json = File.ReadAllText($"{path}{Key}.json");
        var data = JsonConvert.DeserializeObject<T>(
            json
        );
        return data;
    }

    public void Serialize<T>(T data, string path)
    {
        try
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string tempFile = $"{path}{Key}.json.tmp";
            string finalFile = $"{path}{Key}.json";

            File.WriteAllText(tempFile, json);

            if (File.Exists(finalFile))
                File.Delete(finalFile);

            File.Move(tempFile, finalFile);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save {Key}: {e}");
        }
    }

    public void Reset(string slotPath)
    {
        string finalFile = $"{slotPath}{Key}.json";
        if (File.Exists(finalFile))
            File.Delete(finalFile);
    }
}

public abstract class XMLSaveModule : ISaveModule
{
    public abstract string Key { get; }

    public Type DataType => GetType();

    public abstract void Load(string path);
    public abstract void Save(string path);

    public T Deserialize<T>(string path)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var fs = File.OpenRead($"{path}{Key}.xml");
        return (T)serializer.Deserialize(fs);
    }

    public void Serialize<T>(T data, string path)
    {
        try
        {
            string tempFile = $"{path}{Key}.xml.tmp";
            string finalFile = $"{path}{Key}.xml";

            var serializer = new XmlSerializer(typeof(T));

            using (var fsTemp = File.Create(tempFile))
            {
                serializer.Serialize(fsTemp, data);
            }

            if (File.Exists(finalFile))
                File.Delete(finalFile);

            File.Move(tempFile, finalFile);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save {Key}: {e}");
        }
    }
    public void Reset(string slotPath)
    {
        string finalFile = $"{slotPath}{Key}.xml";
        if (File.Exists(finalFile))
            File.Delete(finalFile);
    }

}

public struct SaveManifestData
{
    public int saveFormatVersion;
    public string gameVersion;
    public DateTime dateTime;
    public float currPlaySec;
    public string sceneName;
    public string saveName;
}
public class SaveManifest : XMLSaveModule
{
    public override string Key => "SaveManifest";

    public SaveManifestData saveManifest;
    public float currPlaySec;

    public void UpdatePlaySec()
    {
        currPlaySec += Time.unscaledDeltaTime;
    }

    public override void Save(string path)
    {
        saveManifest = new SaveManifestData() { 
            saveFormatVersion = 1,
            gameVersion = Application.version,
            dateTime = DateTime.UtcNow,
            currPlaySec = currPlaySec,
            sceneName = SceneManager.GetActiveScene().name,
            saveName = DateTime.UtcNow.ToString("f")
        };

        Serialize(saveManifest,path);
    }

    public override void Load(string path)
    {
        saveManifest = Deserialize<SaveManifestData>(path);
        currPlaySec = saveManifest.currPlaySec;
    }
}
public class GlobalSaves : JsonSaveModule, KVPSaves
{
    public Dictionary<string, string> globalStates = new Dictionary<string, string>();
    public Action<string, string> onGlobalStateChange;

    public override string Key => "GlobalState";

    public SaveManager SetData(string key, string value)
    {
        if (!Exist(key))
            globalStates.Add(key, value);

        globalStates[key] = value;
        onGlobalStateChange?.Invoke(key, value);

        return SaveManager.Instance;
    }
    public SaveManager DeleteData(string key)
    {
        if (Exist(key))
            globalStates.Remove(key);

        return SaveManager.Instance;
    }
    public bool Exist(string key)
    {
        return globalStates.ContainsKey(key);
    }
    public string GetData(string key)
    {
        return globalStates[key];
    }

    public bool TryGetData(string key, out string data)
    {
        return globalStates.TryGetValue(key, out data);
    }

    public override void Load(string path)
    {
        globalStates = Deserialize<Dictionary<string, string>>(path);
    }

    public override void Save(string path)
    {
        Serialize(globalStates, path);
    }
}


public class WorldObjectsStateSave : JsonSaveModule, KVPSaves
{

    public Dictionary<string,string> worldFlags = new Dictionary<string, string>();

    public override string Key => "WorldState";

    public SaveManager SetData(string key,string value)
    {
        worldFlags[key] = value;
        return SaveManager.Instance;
    }
    public bool Exist(string key)
    {
        return worldFlags.ContainsKey(key);  
    }
    public string GetData(string key)
    {
        return worldFlags[key];
    }
    public override void Load(string path)
    {
        worldFlags = Deserialize<Dictionary<string, string>>(path);
    }

    public override void Save(string path)
    {
        Serialize(worldFlags, path);
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
            modulesTemp?.Clear();
            _modules = value;
            for (int i = 0; i < _modules.Length; i++)
            {
                modulesTemp?.Add(_modules[i].DataType, _modules[i]);
            }
        } }

    private Dictionary<Type, ISaveModule> modulesTemp { get; set; } = new();

    public T GetModule<T>()
        => (T)modulesTemp[typeof(T)];

    public T GetModule<T>(T type)
    => (T)modulesTemp[type.GetType()];

    static readonly string BasePath =
        Application.persistentDataPath + "/saves/";

    public int CurrSlot;

    private SaveManager()
    {
        Directory.CreateDirectory(BasePath);
    }

    public void SaveModule<T>() where T : ISaveModule
    {
        var slotPath = $"{BasePath}slot_{CurrSlot}/";
        if (!Directory.Exists(slotPath))
            Directory.CreateDirectory(slotPath);

        GetModule<T>().Save(slotPath);
    }

    public void Save()
    {
        var slotPath = $"{BasePath}slot_{CurrSlot}/";
        if(!Directory.Exists(slotPath)) 
            Directory.CreateDirectory(slotPath);

        foreach (var m in _modules)
        {
            m.Save(slotPath);
        }
    }

    public void Reset()
    {
        var slotPath = $"{BasePath}slot_{CurrSlot}/";
        if (!Directory.Exists(slotPath))
            Directory.CreateDirectory(slotPath);

        foreach (var m in _modules)
        {
            m.Reset(slotPath);
        }
    }

    public void Load()
    {
        var slotPath = $"{BasePath}slot_{CurrSlot}/";

        foreach (var m in _modules)
        {
            m.Load(slotPath);
        }
    }
}
