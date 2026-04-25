using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class PausePage : MonoBehaviour
{
    public CanvasGroup CanvasGroup;

    private bool isShow = false;

    private Tween showTween;
    public void Start()
    {
        InputManager.inputActions.UI.Settings.performed += ShowHideAction;
    }

    public void OnDestroy()
    {
        InputManager.inputActions.UI.Settings.performed -= ShowHideAction;
    }

    public void ShowHideAction(InputAction.CallbackContext clx)
    {
        isShow = !isShow;
        ShowHide(isShow);
    }

    public void MainMenu()
    {
        TimeManager.UnFreeze();
        GameModeManager.Instance.HandleStartRequest(GameModeManager.Instance.mainMenuMode);
    }

    public void ShowHide(bool isShow)
    {
        showTween?.Kill();
        showTween = CanvasGroup.DOFade(isShow ? 1 : 0,0.2f).SetUpdate(true);
        
        CanvasGroup.interactable = isShow;
        CanvasGroup.blocksRaycasts = isShow;
        TimeManager.FreezeUnFreeze(isShow);
    }
}
