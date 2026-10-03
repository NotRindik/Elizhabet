using DG.Tweening;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScreenTipManager : MonoBehaviour
{
    public static ScreenTipManager Instance { get; private set; }

    public Image image;
    public TMP_Text header, body;
    public CanvasGroup canvasGroup,buttonCanvasGroup;
    public UnityEngine.UI.Button closeButton;

    [Header("Animation")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float offsetX = 1200f;

    private RectTransform rect;
    private Sequence seq;
    private bool isVisible;

    private PlayerManipulator Manipulator => ContextManager.Instance.player.GetComponent<PlayerManipulator>();

    private void Awake()
    {
        Instance = this;
        rect = (RectTransform)canvasGroup.transform;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
        closeButton.onClick.AddListener(Hide);
    }

    private void OnDestroy()
    {
        seq?.Kill();
        closeButton.onClick.RemoveListener(Hide);
        if (Instance == this) Instance = null;
    }

    public void Show(TipData data)
    {
        image.sprite = data.icon;
        header.text = data.header;
        body.text = data.body;

        Manipulator.FreezePlayer(true);
        isVisible = true;

        seq?.Kill();
        
        rect.anchoredPosition = new Vector2(-offsetX, 0f);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        seq = DOTween.Sequence().SetUpdate(true)
            .Join(rect.DOAnchorPos(Vector2.zero, duration).SetEase(Ease.OutCubic))
            .Join(canvasGroup.DOFade(1f, duration)).OnComplete(() =>
                {
                    buttonCanvasGroup.DOFade(1, 0.3f);
                    buttonCanvasGroup.blocksRaycasts = true;
                    buttonCanvasGroup.interactable = true;
                }
            );
    }

    public void Hide()
    {
        if (!isVisible) return;
        isVisible = false;

        Manipulator.FreezePlayer(false);

        seq?.Kill();

        canvasGroup.interactable = false;
        
        seq = DOTween.Sequence().SetUpdate(true)
            .Join(rect.DOAnchorPos(new Vector2(offsetX, 0f), duration).SetEase(Ease.InCubic))
            .Join(canvasGroup.DOFade(0f, duration))
            .OnComplete(() =>
                {
                    canvasGroup.blocksRaycasts = false;
                    buttonCanvasGroup.DOFade(1, 0.3f);
                    
                    buttonCanvasGroup.blocksRaycasts = false;
                    buttonCanvasGroup.interactable = false;
                }
            );
    }
}

[System.Serializable]
public struct TipData
{
    public Sprite icon;
    public string header;
    [TextArea] public string body;
}