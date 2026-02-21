using UnityEngine;


[DefaultExecutionOrder(1000)]
public class DestroySave : MonoBehaviour
{
    public string localKey;

    public string BuildedSave => WorldKeyBuilder.Build(this,localKey);

    private void Awake()
    {
        if (SaveManager.Instance.GetModule<GlobalSaves>().Exist(BuildedSave))
        {
            gameObject.SetActive(false);
        }
    }
    public void SaveDestuction()
    {
        SaveManager.Instance.GetModule<GlobalSaves>().SetData(BuildedSave,"1").Save();
    }
}
