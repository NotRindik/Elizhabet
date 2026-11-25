using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public struct RepositionState
{
    public Vector3 localPosition;
    public Quaternion rotation;
}
[DefaultExecutionOrder(1000)]
public class RepositionSave : MonoBehaviour
{
    public RepositionState repositionState = new();
    public string localKey;
    public string BuildedKey => WorldKeyBuilder.Build(this,localKey);

    public BetterEvent OnLoaded,OnSaveState;

    public void Start()
    {
        if (SaveManager.Instance.GetModule<WorldScope>().HasFlag(this, localKey))
        {
            transform.localPosition = repositionState.localPosition;
            transform.rotation = repositionState.rotation;
            OnLoaded.Invoke();
        }
    }

    public void SaveState()
    {
        SaveManager.Instance.GetModule<WorldScope>().SetFlag(this, localKey);
        SaveService.Instance.RequestSave();
        OnSaveState.Invoke();
    }
}
