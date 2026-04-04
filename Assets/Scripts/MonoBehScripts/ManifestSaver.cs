using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ManifestSaver : MonoBehaviour, IGameService
{
    public static ManifestSaver Instance;

    public float playtime;  

    public GameModeManager gameModeManager => GameModeManager.Instance;

    public void Init()
    {
        if (Instance == null)
            Instance = this;
        playtime = SaveManager.Instance.GetModule<SaveManifest>().Data.currPlaySec;

        SaveFirstTimeOnStart();
    }
    public void SaveFirstTimeOnStart()
    {
        StartCoroutine(SaveProcess());
    }

    public IEnumerator SaveProcess()
    {
        yield return new WaitUntil(() => GameModeManager.Instance.CurrMode is StoryMode);
        yield return new WaitUntil(() => !TransitionEffect.Instance.IsBlending);
        yield return new WaitForSeconds(1);

        var global = SaveManager.Instance.GetModule<GlobalSaves>();
        if (!global.Exist("FirstTime"))
        {
            Save();
            global.SetData("FirstTime", "1");
            SaveManager.Instance.SaveModule<GlobalSaves>();
        }
    }

    public void Update()
    {
        if (gameModeManager.CurrMode is StoryMode)
        {
            playtime += Time.unscaledDeltaTime;
        }
    }
    IEnumerator CaptureScreenshot(string path)
    {
        yield return new WaitForEndOfFrame();

        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] jpg = tex.EncodeToJPG();

        File.WriteAllBytes(path, jpg);

        Destroy(tex);
    }
    private void OnDestroy()
    {
        Instance = null;
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
            sceneName = SceneLoader.SceneFlow.CurrentScene.name,
            saveName = DateTime.UtcNow.ToString("f"),
            screenshotName = "Screen.jpg"
        };

        SaveManager.Instance.GetModule<SaveManifest>().SetData(data);
        SaveManager.Instance.SaveModule<SaveManifest>();
    }
}