using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;


[Serializable]
public abstract class SaveWhen
{
    public virtual void Enable(AutoSaveManager manager) { }
    public virtual void Disable(AutoSaveManager manager) { }
    public virtual void OnSaved(AutoSaveManager manager) { }
    public abstract bool ShouldSave(AutoSaveManager manager);
}

[Serializable]
public abstract class SaveBlocker
{
    public virtual void Enable(AutoSaveManager manager) { }
    public virtual void Disable(AutoSaveManager manager) { }
    public abstract bool IsBlocking(AutoSaveManager manager);
}

public class AutoSaveManager : MonoBehaviour
{
    public static AutoSaveManager Instance { get; private set; }

    [SerializeField, Min(0.05f)] private float checkInterval = 0.25f;
    [SerializeField, Min(0f)] private float minSaveGap = 10f;

    [SerializeReference, ListDrawerSettings(DraggableItems = true, ShowFoldout = false)]
    private List<SaveWhen> triggers = new();

    [SerializeReference, ListDrawerSettings(DraggableItems = true, ShowFoldout = false)]
    private List<SaveBlocker> blockers = new();

    private readonly HashSet<string> signals = new();
    private Func<bool> blockCheck;
    private float nextCheck;
    private float lastSaveTime;
    private bool pending;

    public bool HasPending => pending;
    public float TimeSinceLastSave => Time.unscaledTime - lastSaveTime;

    public static void Raise(string signalId)
    {
        if (Instance != null)
            Instance.signals.Add(signalId);
    }

    public bool HasSignal(string signalId) => signals.Contains(signalId);
    public void ClearSignal(string signalId) => signals.Remove(signalId);

    public bool IsBlocked()
    {
        for (int i = 0; i < blockers.Count; i++)
        {
            if (blockers[i] != null && blockers[i].IsBlocking(this))
                return true;
        }
        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        lastSaveTime = Time.unscaledTime;
        nextCheck = 0f;

        foreach (var t in triggers)
            t?.Enable(this);
        foreach (var b in blockers)
            b?.Enable(this);

        blockCheck = () => !IsBlocked();
        SaveService.Instance.ExtraCanSave = blockCheck;
        SaveService.Instance.Saved += HandleSaved;
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        SaveService.Instance.Saved -= HandleSaved;
        if (SaveService.Instance.ExtraCanSave == blockCheck)
            SaveService.Instance.ExtraCanSave = null;

        foreach (var t in triggers)
            t?.Disable(this);
        foreach (var b in blockers)
            b?.Disable(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        SaveManager.Instance.Flush();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;

        nextCheck = Time.unscaledTime + checkInterval;

        if (!pending)
            pending = EvaluateTriggers();

        if (!pending)
            return;

        if (TimeSinceLastSave < minSaveGap)
            return;

        if (SaveManager.Instance.IsWriting)
            return;

        if (!SaveService.Instance.CanSave)
            return;

        Fire();
    }

    private bool EvaluateTriggers()
    {
        for (int i = 0; i < triggers.Count; i++)
        {
            if (triggers[i] != null && triggers[i].ShouldSave(this))
                return true;
        }
        return false;
    }

    private void Fire()
    {
        if (!SaveService.Instance.TrySave(SaveReason.Auto))
            return;

        pending = false;
        lastSaveTime = Time.unscaledTime;
        NotifySaved();
    }

    private void HandleSaved(SaveReason reason, bool ok)
    {
        if (reason != SaveReason.Manual || !ok)
            return;

        pending = false;
        lastSaveTime = Time.unscaledTime;
        NotifySaved();
    }

    private void NotifySaved()
    {
        foreach (var t in triggers)
            t?.OnSaved(this);
    }
}

[Serializable]
public class SaveOnInterval : SaveWhen
{
    [Min(1f)] public float seconds = 120f;

    public override bool ShouldSave(AutoSaveManager manager) => manager.TimeSinceLastSave >= seconds;
}

[Serializable]
public class SaveOnSceneChange : SaveWhen
{
    [NonSerialized] private bool changed;

    public override void Enable(AutoSaveManager manager)
    {
        changed = false;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    public override void Disable(AutoSaveManager manager)
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void OnSceneChanged(Scene from, Scene to) => changed = true;

    public override bool ShouldSave(AutoSaveManager manager) => changed;

    public override void OnSaved(AutoSaveManager manager) => changed = false;
}

[Serializable]
public class SaveOnSignal : SaveWhen
{
    public string signalId = "combat_end";

    public override bool ShouldSave(AutoSaveManager manager) => manager.HasSignal(signalId);

    public override void OnSaved(AutoSaveManager manager) => manager.ClearSignal(signalId);
}

[Serializable]
public class BlockOnScenes : SaveBlocker
{
    public List<string> sceneNames = new();

    public override bool IsBlocking(AutoSaveManager manager) => sceneNames.Contains(SceneManager.GetActiveScene().name);
}

[Serializable]
public class BlockWhenPaused : SaveBlocker
{
    public override bool IsBlocking(AutoSaveManager manager) => Time.timeScale <= 0f;
}
