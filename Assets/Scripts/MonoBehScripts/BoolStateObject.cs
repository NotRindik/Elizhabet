using UnityEngine;
using Sirenix.OdinInspector;

public abstract class BoolStateObject : SerializedMonoBehaviour
{
    [Header("Save")]
    [SerializeField] private string _localKey = "used";
    [SerializeField] private bool _notSave;

    protected bool IsUsed { get; private set; }

    private WorldScope WorldSave => SaveManager.Instance.GetModule<WorldScope>();
    protected string SaveKey => WorldKeyBuilder.Build(this, _localKey);

    protected virtual void Start() => Load();

    public void SetNotSave(bool value) => _notSave = value;

    protected void Load()
    {
        if (!WorldSave.HasFlag(SaveKey)) return;
        IsUsed = WorldSave.HasFlag(SaveKey);
        OnLoaded();
    }

    public void Save(bool value)
    {
        IsUsed = value;
        if (_notSave) return;
        WorldSave.SetFlag(SaveKey);
        SaveManager.Instance.SaveModule<WorldScope>();
    }

    protected abstract void OnLoaded();

#if UNITY_EDITOR
    [Button("CLEAR SAVE", ButtonSizes.Small, ButtonStyle.Box)]
    private void ClearSave()
    {
        var worldSave = new WorldScope();
        var key = WorldKeyBuilder.Build(this, _localKey);
        worldSave.Load(SaveManager.Instance.SlotPath);

        if (!worldSave.HasFlag(key))
        {
            Debug.Log($"[{GetType().Name}] No save found for key: {key}");
            return;
        }

        worldSave.ClearFlag(key);
        worldSave.Save(SaveManager.Instance.SlotPath);
        IsUsed = false;

        Debug.Log($"[{GetType().Name}] Save cleared: {key}");
    }
#endif
}