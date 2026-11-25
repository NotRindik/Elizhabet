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
        if (!WorldSave.HasFlag(this, _localKey)) return;
        IsUsed = true;
        OnLoaded();
    }

    public void Save(bool value)
    {
        IsUsed = value;
        if (_notSave) return;
        WorldSave.SetFlag(this, _localKey);
    }

    protected abstract void OnLoaded();

#if UNITY_EDITOR
    [Button("CLEAR SAVE", ButtonSizes.Small, ButtonStyle.Box)]
    private void ClearSave()
    {
        var worldSave = new WorldScope();
        var key = WorldKeyBuilder.Build(this, _localKey);
        worldSave.Load(SaveManager.Instance.SlotPath);

        if (!worldSave.HasFlag(this, _localKey))
        {
            Debug.Log($"[{GetType().Name}] No save found for key: {key}");
            return;
        }

        worldSave.ClearFlag(this, _localKey);
        IsUsed = false;

        Debug.Log($"[{GetType().Name}] Save cleared: {key}");
    }
#endif
}