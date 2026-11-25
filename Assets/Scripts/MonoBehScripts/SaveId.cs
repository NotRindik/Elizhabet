using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(-1000)]
public class SaveId : MonoBehaviour
{
    [SerializeField] private string id;

    private static readonly Dictionary<string, SaveId> Live = new();
    private bool _duplicate;
    private bool _checked;

    public string Id => id;

    public bool HasId
    {
        get
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (!_checked)
                Register();
            return !_duplicate;
        }
    }

    private string LiveKey => $"{gameObject.scene.name}/{id}";

    private void Awake()
    {
        if (!string.IsNullOrEmpty(id))
            Register();
    }

    private void Register()
    {
        _checked = true;
        if (!Application.isPlaying)
            return;
        var key = LiveKey;
        if (Live.TryGetValue(key, out var other) && other != null && other != this)
        {
            _duplicate = true;
            return;
        }
        Live[key] = this;
    }

    private void OnDestroy()
    {
        if (_duplicate || string.IsNullOrEmpty(id))
            return;
        var key = LiveKey;
        if (Live.TryGetValue(key, out var other) && other == this)
            Live.Remove(key);
    }

#if UNITY_EDITOR
    public void EditorSetId(string value)
    {
        id = value;
        _checked = false;
        _duplicate = false;
    }
#endif
}
