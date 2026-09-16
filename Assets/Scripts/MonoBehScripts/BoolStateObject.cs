using UnityEngine;
using Sirenix.OdinInspector;

public abstract class BoolStateObject : SerializedMonoBehaviour
{
    [Header("Save")]
    [SerializeField] private string _localKey = "used";
    [SerializeField] private bool _notSave;

    protected bool IsUsed { get; private set; }

    private WorldObjectsStateSave WorldSave => SaveManager.Instance.GetModule<WorldObjectsStateSave>();
    protected string SaveKey => WorldKeyBuilder.Build(this, _localKey);

    protected virtual void Start() => Load();

    public void SetNotSave(bool value) => _notSave = value;

    protected void Load()
    {
        if (!WorldSave.Exist(SaveKey)) return;
        IsUsed = WorldSave.GetData(SaveKey) == "1";
        OnLoaded();
    }

    public void Save(bool value)
    {
        IsUsed = value;
        if (_notSave) return;
        WorldSave.SetData(SaveKey, value ? "1" : "0");
        SaveManager.Instance.SaveModule<WorldObjectsStateSave>();
    }

    protected abstract void OnLoaded();

#if UNITY_EDITOR
    [Button("CLEAR SAVE", ButtonSizes.Small, ButtonStyle.Box)]
    private void ClearSave()
    {
        var worldSave = new WorldObjectsStateSave();
        var key = WorldKeyBuilder.Build(this, _localKey);
        worldSave.Load(SaveManager.Instance.SlotPath);

        if (!worldSave.Exist(key))
        {
            Debug.Log($"[{GetType().Name}] No save found for key: {key}");
            return;
        }

        worldSave.worldFlags.Remove(key);
        worldSave.Save(SaveManager.Instance.SlotPath);
        IsUsed = false;

        Debug.Log($"[{GetType().Name}] Save cleared: {key}");
    }
#endif
}