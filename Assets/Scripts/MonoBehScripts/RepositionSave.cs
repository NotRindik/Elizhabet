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

    public BetterEvent OnLoaded;

    public void Awake()
    {
        if (SaveManager.Instance.GetModule<GlobalSaves>().Exist(BuildedKey))
        {
            transform.localPosition = repositionState.localPosition;
            transform.rotation = repositionState.rotation;
            OnLoaded.Invoke();
        }
    }

    public void SaveState()
    {
        SaveManager.Instance.GetModule<GlobalSaves>().SetData(BuildedKey, "1").Save();
    }
}
