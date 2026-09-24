using System;
using UnityEngine;
using UnityEngine.Video;
using Sirenix.OdinInspector;
using UnityEngine.UI;

[RequireComponent(typeof(VideoPlayer))]
public class VideoPlayerManager : SerializedMonoBehaviour
{
    private VideoPlayer player;

    public RawImage OutputImage;

    [Title("Render Texture")]
    public int textureWidth = 1920;
    public int textureHeight = 1080;
    public RenderTextureFormat textureFormat = RenderTextureFormat.ARGB32;

    private RenderTexture videoTexture;

    public bool playOnce = true;
    private bool played;

    public string localKey = "Video";

    private string Key => WorldKeyBuilder.Build(this, localKey);

    public BetterEvent onEnd;
    public BetterEvent onStart;

    private VideoPlayer.EventHandler EndHandle;
    private VideoPlayer.EventHandler StartHandle;

    private void Awake()
    {
        player ??= GetComponent<VideoPlayer>();

        CreateVideoTexture();

        EndHandle = c =>
        {
            RenderTexture.active = videoTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = null;

            onEnd.Invoke();
        };
    }

    private void CreateVideoTexture()
    {
        videoTexture = new RenderTexture(textureWidth, textureHeight, 0, textureFormat)
        {
            name = $"{gameObject.name}_VideoRT"
        };
        videoTexture.Create();

        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = videoTexture;

        if (OutputImage != null)
            OutputImage.texture = videoTexture;
    }

    private void OnEnable()
    {
        player.loopPointReached += EndHandle;
        player.started += StartHandle;
    }

    private void Start()
    {
        var global = SaveManager.Instance.GetModule<GlobalSaves>();

        bool exist = global.Exist(Key);
        if (exist)
            played = global.GetData(Key) == "1";
        else
            played = false;

        if (playOnce)
        {
            if (!played)
            {
                Play();
                global.SetData(Key, "1").Save();
            }
        }
        else
        {
            Play();
        }
    }

    private void Play()
    {
        player.time = 0;
        player.Play();
        onStart.Invoke();
    }

    private void OnDisable()
    {
        player.loopPointReached -= EndHandle;
        player.started -= StartHandle;
    }

    private void OnDestroy()
    {
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
    }
}