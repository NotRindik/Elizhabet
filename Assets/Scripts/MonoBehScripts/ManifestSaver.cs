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

    public Coroutine saveProcess;

    public void Init()
    {
        if (Instance == null)
            Instance = this;
        playtime = SaveManager.Instance.GetModule<SaveManifest>().Data.currPlaySec;

        GameModeManager.Instance.OnGameModeChange += OnGameModeChange;
    }

    public void OnGameModeChange(IGameMode mode)
    {
        if(mode is not StoryMode)
        {
            return;
        }

        SaveFirstTimeOnStart();
    }
    public void SaveFirstTimeOnStart()
    {
        if (saveProcess != null)
            StopCoroutine(saveProcess);

        saveProcess = StartCoroutine(SaveProcess());
    }

    public IEnumerator SaveProcess()
    {
        yield return new WaitUntil(() => GameModeManager.Instance.CurrMode is StoryMode);
        yield return new WaitUntil(() => !TransitionEffect.Instance.IsBlending);
        yield return new WaitForSeconds(1);

        var global = SaveManager.Instance.GetModule<GlobalSaves>();
        if (!global.Exist("FirstTime"))
        {
            Debug.Log("Save ManifestFirstTime");
            Save();
            global.SetData("FirstTime", "1");
            SaveManager.Instance.SaveModule<GlobalSaves>();
        }
        saveProcess = null;
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
        GameModeManager.Instance.OnGameModeChange -= OnGameModeChange;
        Instance = null;
    }

    public void PrepareData()
    {
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
    }
    public void Save()
    {
        string screenPath = $"{SaveManager.Instance.SlotPath}Screen.jpg";
        StartCoroutine(CaptureScreenshot(screenPath));
        PrepareData();
        SaveManager.Instance.SaveModule<SaveManifest>();
    }
}