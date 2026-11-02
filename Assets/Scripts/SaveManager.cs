using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Systems;
using UnityEngine;


public interface KVPSaves
{
    public bool Exist(string key);
}

public readonly struct SaveFile
{
    public readonly string Name;
    public readonly byte[] Data;

    public SaveFile(string name, byte[] data)
    {
        Name = name;
        Data = data;
    }
}

public interface ISaveModule
{
    string Key { get; }
    System.Type DataType { get; }

    public SaveFile Capture();
    public void Load(string slotPath);
    public void Reset(string slotPath);
    public void ClearData();
}

public abstract class JsonSaveModule : ISaveModule
{
    public abstract string Key { get; }

    public Type DataType => GetType();

    private static JsonSerializerSettings BuildSettings() => new()
    {
        ContractResolver = new ComponentSaveContractResolver(),
        TypeNameHandling = TypeNameHandling.Auto,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    };

    public abstract SaveFile Capture();
    public abstract void Load(string path);
    public abstract void ClearData();

    public T DeserializeOrDefault<T>(string path) where T : new()
    {
        string file = $"{path}{Key}.json";

        if (!File.Exists(file))
            return new T();

        var json = File.ReadAllText(file);
        var result = JsonConvert.DeserializeObject<T>(json, BuildSettings());
        return result == null ? new T() : result;
    }

    public SaveFile ToFile<T>(T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented, BuildSettings());
        return new SaveFile($"{Key}.json", Encoding.UTF8.GetBytes(json));
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

    public abstract SaveFile Capture();
    public abstract void Load(string path);
    public abstract void ClearData();

    public T DeserializeOrDefault<T>(string path) where T : new()
    {
        string file = $"{path}{Key}.xml";
        if (!File.Exists(file))
            return new();

        var serializer = new XmlSerializer(typeof(T));
        using var fs = File.OpenRead(file);
        return (T)serializer.Deserialize(fs);
    }

    public SaveFile ToFile<T>(T data)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var ms = new MemoryStream();
        serializer.Serialize(ms, data);
        return new SaveFile($"{Key}.xml", ms.ToArray());
    }

    public void Reset(string slotPath)
    {
        string finalFile = $"{slotPath}{Key}.xml";
        if (File.Exists(finalFile))
            File.Delete(finalFile);
    }
}

[Serializable]
public class ItemInstanceSave
{
    public Dictionary<Type, ISaveSerialize> SerializedComponent = new();
}

[Serializable]
public class ItemStackSave
{
    public string itemName;
    public List<ItemInstanceSave> instances = new();
}

[Serializable]
public class InventorySave
{
    public List<ItemStackSave> hotBar = new();
    public List<ItemStackSave> storage = new();
    public List<ItemStackSave> armor = new();
    public List<ItemStackSave> accessories = new();
}

[Serializable]
public class PlayerSaveData
{
    public InventorySave inventory = new();
    public HashSet<AbilityType> openedAbility = new();
    public string lastSaveCapsuleId;
    public string lastSaveScene;
    public Dictionary<string, string> pickedUpItems = new();
}

public class PlayerSave : JsonSaveModule
{
    public override string Key => "Player";

    public PlayerSaveData Data { get; private set; } = new();

    public override void Load(string path) => Data = DeserializeOrDefault<PlayerSaveData>(path);
    public override SaveFile Capture() => ToFile(Data);
    public override void ClearData() => Data = new();
}

public struct SaveManifestData
{
    public int saveFormatVersion;
    public string gameVersion;
    public DateTime dateTime;
    public float currPlaySec;
    public string sceneName;
    public string saveName;

    public string screenshotName;
}

public class SaveManifest : XMLSaveModule
{
    public override string Key => "SaveManifest";

    public SaveManifestData Data { get; private set; }

    public void SetData(SaveManifestData data) => Data = data;

    public override SaveFile Capture() => ToFile(Data);

    public override void Load(string path)
    {
        Data = DeserializeOrDefault<SaveManifestData>(path);
    }

    public override void ClearData() => Data = default;

    public SaveManifestData GetData(string path)
    {
        return DeserializeOrDefault<SaveManifestData>(path);
    }
}

public class GlobalSaves : JsonSaveModule, KVPSaves
{
    public Dictionary<string, string> globalStates = new Dictionary<string, string>();
    public Action<string, string> onGlobalStateChange;

    public override string Key => "GlobalState";

    public SaveManager SetData(string key, string value)
    {
        globalStates[key] = value;
        onGlobalStateChange?.Invoke(key, value);

        return SaveManager.Instance;
    }

    public SaveManager DeleteData(string key)
    {
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
        globalStates = DeserializeOrDefault<Dictionary<string, string>>(path);
    }

    public override SaveFile Capture() => ToFile(globalStates);

    public override void ClearData() => globalStates = new Dictionary<string, string>();
}

public class WorldStateData
{
    public HashSet<string> flags = new();
    public Dictionary<string, int> counters = new();
    public Dictionary<string, bool> states = new();
    public Dictionary<string, string> stringState = new();
}

public class WorldScope : JsonSaveModule, KVPSaves
{
    public WorldStateData Data { get; private set; } = new();

    public override string Key => "WORLD SAVES";

    public bool HasFlag(string key) => Data.flags.Contains(key);
    public void SetFlag(string key) => Data.flags.Add(key);
    public void ClearFlag(string key) => Data.flags.Remove(key);

    public int GetCounter(string key) => Data.counters.TryGetValue(key, out var v) ? v : 0;
    public void SetCounter(string key, int value) => Data.counters[key] = value;

    public bool GetState(string key, bool fallback = false) => Data.states.TryGetValue(key, out var v) ? v : fallback;
    public void SetState(string key, bool value) => Data.states[key] = value;

    public string GetStringState(string key, string fallback = "") => Data.stringState.TryGetValue(key, out var v) ? v : fallback;
    public void SetStringState(string key, string value) => Data.stringState[key] = value;

    public override SaveFile Capture() => ToFile(Data);

    public override void Load(string path)
    {
        Data = DeserializeOrDefault<WorldStateData>(path) ?? new WorldStateData();
    }

    public override void ClearData() => Data = new WorldStateData();

    public bool Exist(string key)
    {
        return Data.stringState.ContainsKey(key);
    }
}

public static class WorldKeyBuilder
{
    public static string Build(Component c, string localKey)
    {
        return $"{SceneLoader.SceneFlow.CurrentScene.name}/" +
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
    public ISaveModule[] Modules
    {
        get => _modules;
        set
        {
            modulesTemp.Clear();
            _modules = value;
            for (int i = 0; i < _modules.Length; i++)
            {
                modulesTemp.Add(_modules[i].DataType, _modules[i]);
            }
        }
    }

    private Dictionary<Type, ISaveModule> modulesTemp { get; set; } = new();

    public T GetModule<T>()
        => (T)modulesTemp[typeof(T)];

    public T GetModule<T>(T type)
        => (T)modulesTemp[type.GetType()];

    static readonly string BasePath =
        Application.persistentDataPath + "/saves/";

    public string SlotPath => $"{BasePath}slot_{CurrSlot}/";

    public int CurrSlot;

    private readonly object _lock = new();
    private readonly SynchronizationContext _mainContext;
    private Task _tail = Task.CompletedTask;
    private int _pending;

    public bool IsWriting => Volatile.Read(ref _pending) > 0;

    private SaveManager()
    {
        _mainContext = SynchronizationContext.Current;
        Directory.CreateDirectory(BasePath);
    }

    public string GetSlotPath(int slot = 0) => $"{BasePath}slot_{slot}/";

    private SaveFile[] CaptureAll()
    {
        var files = new SaveFile[_modules.Length];
        for (int i = 0; i < _modules.Length; i++)
            files[i] = _modules[i].Capture();
        return files;
    }

    public void SaveAsync(Action<bool> onComplete = null)
    {
        var files = CaptureAll();
        int slot = CurrSlot;

        Interlocked.Increment(ref _pending);

        lock (_lock)
        {
            _tail = _tail.ContinueWith(_ =>
            {
                bool ok = WriteSlot(slot, files);
                Interlocked.Decrement(ref _pending);
                if (onComplete != null)
                    Notify(onComplete, ok);
            }, TaskScheduler.Default);
        }
    }

    public bool Save()
    {
        var files = CaptureAll();
        Flush();
        return WriteSlot(CurrSlot, files);
    }

    public void SaveModule<T>() where T : ISaveModule
    {
        var file = GetModule<T>().Capture();
        Flush();

        var slotDir = $"{BasePath}slot_{CurrSlot}";
        Directory.CreateDirectory(slotDir);

        var finalPath = Path.Combine(slotDir, file.Name);
        var tempPath = finalPath + ".tmp";

        File.WriteAllBytes(tempPath, file.Data);
        if (File.Exists(finalPath))
            File.Delete(finalPath);
        File.Move(tempPath, finalPath);
    }

    public void Flush()
    {
        Task tail;
        lock (_lock)
        {
            tail = _tail;
        }
        tail.Wait();
    }

    private void Notify(Action<bool> callback, bool ok)
    {
        if (_mainContext != null)
            _mainContext.Post(_ => callback(ok), null);
        else
            callback(ok);
    }

    private static bool WriteSlot(int slot, SaveFile[] files)
    {
        string slotDir = $"{BasePath}slot_{slot}";
        string staging = $"{slotDir}_staging";
        string backup = $"{slotDir}_old";

        try
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            var written = new HashSet<string>();
            foreach (var f in files)
            {
                File.WriteAllBytes(Path.Combine(staging, f.Name), f.Data);
                written.Add(f.Name);
            }

            if (Directory.Exists(slotDir))
            {
                foreach (var existing in Directory.GetFiles(slotDir))
                {
                    var name = Path.GetFileName(existing);
                    if (!written.Contains(name))
                        File.Copy(existing, Path.Combine(staging, name), true);
                }
            }

            if (Directory.Exists(backup))
                Directory.Delete(backup, true);
            if (Directory.Exists(slotDir))
                Directory.Move(slotDir, backup);
            Directory.Move(staging, slotDir);
            if (Directory.Exists(backup))
                Directory.Delete(backup, true);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save failed: {e}");
            try
            {
                if (!Directory.Exists(slotDir) && Directory.Exists(backup))
                    Directory.Move(backup, slotDir);
            }
            catch (Exception restore)
            {
                Debug.LogError($"Save restore failed: {restore}");
            }
            return false;
        }
    }

    public void Recover(int slot)
    {
        string slotDir = $"{BasePath}slot_{slot}";
        string staging = $"{slotDir}_staging";
        string backup = $"{slotDir}_old";

        try
        {
            if (!Directory.Exists(slotDir) && Directory.Exists(backup))
                Directory.Move(backup, slotDir);
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            if (Directory.Exists(backup) && Directory.Exists(slotDir))
                Directory.Delete(backup, true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Save recover failed: {e}");
        }
    }

    public void Reset(int slot)
    {
        Flush();

        var slotPath = GetSlotPath(slot);
        if (!Directory.Exists(slotPath))
            Directory.CreateDirectory(slotPath);

        foreach (var m in _modules)
        {
            m.Reset(slotPath);
        }

        if (slot == CurrSlot)
        {
            foreach (var m in _modules)
                m.ClearData();
        }
    }

    public void Reset()
    {
        Reset(CurrSlot);
    }

    public void Load()
    {
        Flush();
        Recover(CurrSlot);

        Debug.Log($"Save Loaded from slot {CurrSlot}");
        var slotPath = SlotPath;

        foreach (var m in _modules)
        {
            m.Load(slotPath);
        }
    }
}

public static class SaveGate
{
    private static readonly HashSet<object> blockers = new();

    public static bool CanSave => blockers.Count == 0;
    public static event Action Unblocked;

    public static void Block(object source) => blockers.Add(source);

    public static void Unblock(object source)
    {
        if (blockers.Remove(source) && blockers.Count == 0)
            Unblocked?.Invoke();
    }

    public static void Clear()
    {
        blockers.Clear();
    }
}

public enum SaveReason { Auto, Manual }

public class SaveService
{
    static SaveService _instance;
    public static SaveService Instance => _instance ??= new SaveService();

    private bool autoPending;

    public event Action BeforeSave;
    public event Action<SaveReason, bool> Saved;

    private SaveService()
    {
        SaveGate.Unblocked += OnUnblocked;
    }

    public Func<bool> ExtraCanSave;

    public bool CanSave => SaveGate.CanSave && (ExtraCanSave == null || ExtraCanSave());

    public bool TrySave(SaveReason reason)
    {
        if (!CanSave)
        {
            if (reason == SaveReason.Auto)
                autoPending = true;
            return false;
        }

        autoPending = false;
        BeforeSave?.Invoke();
        SaveManager.Instance.SaveAsync(ok => Saved?.Invoke(reason, ok));
        return true;
    }

    public bool SaveNowBlocking()
    {
        if (!CanSave)
            return false;

        autoPending = false;
        BeforeSave?.Invoke();
        return SaveManager.Instance.Save();
    }

    private void OnUnblocked()
    {
        if (autoPending)
            TrySave(SaveReason.Auto);
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class SaveFieldAttribute : Attribute { }

public class ComponentSaveContractResolver : DefaultContractResolver
{
    protected override List<MemberInfo> GetSerializableMembers(Type objectType)
    {
        if (!typeof(ISaveSerialize).IsAssignableFrom(objectType))
            return base.GetSerializableMembers(objectType);

        return objectType
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Cast<MemberInfo>()
            .ToList();
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
    {
        var prop = base.CreateProperty(member, serialization);

        if (member is FieldInfo field && typeof(ISaveSerialize).IsAssignableFrom(field.DeclaringType))
        {
            bool hasSaveField = field.GetCustomAttribute<SaveFieldAttribute>() != null;
            prop.Ignored = !hasSaveField;

            if (hasSaveField)
            {
                prop.Readable = true;
                prop.Writable = true;
                prop.ValueProvider = new ReflectionValueProvider(field);
            }
        }

        return prop;
    }
}