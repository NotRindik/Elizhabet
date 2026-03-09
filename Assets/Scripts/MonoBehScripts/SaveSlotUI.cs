using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    public Image saveImg;
    public TextMeshProUGUI textForNewSave, saveInfo;

    public SaveManifestData data;
    public string slotPath => SaveManager.Instance.GetSlotPath(SlotIndex);

    public int SlotIndex => transform.GetSiblingIndex();

    public void Draw()
    {
        if (data.Equals(default(SaveManifestData)))
        {
            textForNewSave.text = "New Game";
            saveInfo.text = "";
            saveImg.sprite = null;
            saveImg.color = new Color(1, 1, 1, 0);
            return;
        }
        textForNewSave.text = string.Empty;
        saveImg.color = new Color(1, 1, 1, 1);
        saveInfo.text =
            $"{FormatTime(data.currPlaySec)}\n" +
            $"{data.dateTime:yyyy-MM-dd HH:mm}\n" +
            $"{data.sceneName}";

        LoadScreenshot();
    }

    public void DestroySlot()
    {
        SaveManager.Instance.Reset(SlotIndex);
        data = default;
        Draw();
    }

    public void StartGame()
    {
        SaveManager.Instance.CurrSlot = SlotIndex;
        GameModeManager.Instance.HandleStartRequest(new StoryMode());
    }

    void LoadScreenshot()
    {
        string path = $"{slotPath}{data.screenshotName}";

        if (!File.Exists(path))
        {
            saveImg.sprite = null;
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);

        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        RectTransform rt = saveImg.rectTransform;

        float targetWidth = rt.rect.width;
        float targetHeight = rt.rect.height;

        float x = (tex.width - targetWidth) * 0.5f;
        float y = (tex.height - targetHeight) * 0.5f;

        Rect crop = new Rect(x, y, targetWidth, targetHeight);

        saveImg.sprite = Sprite.Create(
            tex,
            crop,
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