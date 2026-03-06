using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    public Image saveImg;
    public TextMeshProUGUI saveName, saveInfo;

    public SaveManifestData data;
    public string slotPath;

    public void Draw()
    {
        if (data.Equals(default(SaveManifestData)))
        {
            saveName.text = "Empty Slot";
            saveInfo.text = "";
            saveImg.sprite = null;
            return;
        }

        saveName.text = data.saveName;

        saveInfo.text =
            $"{FormatTime(data.currPlaySec)}\n" +
            $"{data.dateTime:yyyy-MM-dd HH:mm}\n" +
            $"{data.sceneName}";

        LoadScreenshot();
    }

    void LoadScreenshot()
    {
        string path = Path.Combine(slotPath, data.screenshotName);

        if (!File.Exists(path))
        {
            saveImg.sprite = null;
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);

        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        saveImg.sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    string FormatTime(float seconds)
    {
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        int s = (int)(seconds % 60);

        return $"{h:D2}:{m:D2}:{s:D2}";
    }
}