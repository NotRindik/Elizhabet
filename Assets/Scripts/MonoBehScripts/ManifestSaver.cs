using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ManifestSaver : MonoBehaviour, IGameService
{

    public float playtime;  

    public GameModeManager gameModeManager => GameModeManager.Instance;

    public void Init()
    {
        playtime = SaveManager.Instance.GetModule<SaveManifest>().Data.currPlaySec;
    }

    public void Update()
    {
        if(gameModeManager.CurrMode is StoryMode) 
            playtime += Time.unscaledDeltaTime;
    }
    IEnumerator CaptureScreenshot(string path)
    {
        yield return new WaitForEndOfFrame();

        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] jpg = tex.EncodeToJPG();

        File.WriteAllBytes(path, jpg);

        Destroy(tex);
    }
    public void Save()
    {
        string screenPath = $"{SaveManager.Instance.SlotPath}Screen.jpg";
        StartCoroutine(CaptureScreenshot(screenPath));

        var data = new SaveManifestData()
        {
            saveFormatVersion = 1,
            gameVersion = Application.version,
            dateTime = DateTime.UtcNow,
            currPlaySec = playtime,
            sceneName = SceneManager.GetActiveScene().name,
            saveName = DateTime.UtcNow.ToString("f"),
            screenshotName = "Screen.jpg"
        };

        SaveManager.Instance.GetModule<SaveManifest>().SetData(data);
        SaveManager.Instance.SaveModule<SaveManifest>();
    }
}