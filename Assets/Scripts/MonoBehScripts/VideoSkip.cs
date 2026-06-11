using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

public class VideoSkip : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private VideoPlayerManager videoPlayerManager;

    private Input _inputAction;
    
    private void Start()
    {
        videoPlayer ??= GetComponent<VideoPlayer>();
        videoPlayerManager ??= GetComponent<VideoPlayerManager>();
        _inputAction = new Input();
        _inputAction.Enable();
        
        _inputAction.UI.Skip.performed += SkipVideo;
    }

    private void SkipVideo(InputAction.CallbackContext input)
    {
        
        if(!videoPlayer.isPlaying)
            return;

        ForceSkip();
    }
    private void ForceSkip()
    {
        videoPlayer.Stop();
        videoPlayerManager?.onEnd.Invoke();
    }

    private void OnDisable()
    {
        InputManager.inputActions.UI.Skip.performed -= SkipVideo;
        _inputAction = null;
    }
}
