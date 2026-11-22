using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SavesUI : MonoBehaviour
{
    public SaveSlotUI[] slots = new SaveSlotUI[3];

    public SaveManifest manifest => SaveManager.Instance.GetModule<SaveManifest>();

    public void Start()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].IsCorrupted = !manifest.TryGetData(SaveManager.Instance.GetSlotPath(i), out var data);
            slots[i].data = data;
            slots[i].Draw();
        }
    }
}
