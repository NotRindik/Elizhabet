using System;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class WorldUIElement : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup canvasGroup;

    private WorldUITracker _tracker;
    private Sequence _sequence;
    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _tracker = GetComponent<WorldUITracker>();
    }

    // ── Позиционирование ────────────────────────────────────

    public WorldUIElement SetText(string text)
    {
        if (label != null) label.text = text;
        return this;
    }

    public WorldUIElement SetTarget(Transform target, Vector3 offset = default)
    {
        _tracker?.SetTarget(target, offset);
        return this;
    }

    public WorldUIElement SetPosition(Vector3 worldPos)
    {
        _tracker = GetComponent<WorldUITracker>();
        _tracker?.SetStaticPosition(worldPos);
        return this;
    }

    // ── Текст ───────────────────────────────────────────────

    public WorldUIElement SetFontSize(float size)
    {
        if (label != null) label.fontSize = size;
        return this;
    }

    public WorldUIElement SetColor(Color color)
    {
        if (label != null) label.color = color;
        return this;
    }

    public WorldUIElement SetAlignment(TextAlignmentOptions alignment)
    {
        if (label != null) label.alignment = alignment;
        return this;
    }

    public WorldUIElement SetBold(bool bold)
    {
        if (label != null)
            label.fontStyle = bold
                ? label.fontStyle | FontStyles.Bold
                : label.fontStyle & ~FontStyles.Bold;
        return this;
    }

    public WorldUIElement AppendText(string text)
    {
        if (label != null) label.text += text;
        return this;
    }

    // ── Размер элемента ─────────────────────────────────────

    public WorldUIElement SetSize(Vector2 size)
    {
        _rect.sizeDelta = size;
        return this;
    }

    public WorldUIElement SetSize(float width, float height)
        => SetSize(new Vector2(width, height));

    public WorldUIElement SetScale(float scale)
    {
        transform.localScale = Vector3.one * scale;
        return this;
    }

    // ── Твин взаимодействия ─────────────────────────────────

    public WorldUIElement TweenFontSize(float to, float duration, Ease ease = Ease.OutCubic)
    {
        if (label != null)
            DOTween.To(() => label.fontSize, x => label.fontSize = x, to, duration).SetEase(ease);
        return this;
    }

    public WorldUIElement TweenColor(Color to, float duration, Ease ease = Ease.OutCubic)
    {
        label?.DOColor(to, duration).SetEase(ease);
        return this;
    }

    public WorldUIElement TweenScale(float to, float duration, Ease ease = Ease.OutBack)
    {
        _rect.DOScale(to, duration).SetEase(ease);
        return this;
    }

    public WorldUIElement TweenSize(Vector2 to, float duration, Ease ease = Ease.OutCubic)
    {
        _rect.DOSizeDelta(to, duration).SetEase(ease);
        return this;
    }

    public WorldUIElement Punch(float strength = 0.3f, float duration = 0.4f)
    {
        _rect.DOPunchScale(Vector3.one * strength, duration, 5, 0.5f);
        return this;
    }

    public WorldUIElement Shake(float strength = 5f, float duration = 0.4f)
    {
        _rect.DOShakeAnchorPos(duration, strength);
        return this;
    }

    // ── Show / Hide ─────────────────────────────────────────

    public void Show(float fadeInDuration = 0.3f, Action onComplete = null)
    {
        gameObject.SetActive(true);
        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(canvasGroup.DOFade(1f, fadeInDuration));
        _sequence.OnComplete(() => onComplete?.Invoke());
    }

    public void Hide(float fadeOutDuration = 0.3f, Action onComplete = null)
    {
        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(canvasGroup.DOFade(0f, fadeOutDuration));
        _sequence.OnComplete(() =>
        {
            onComplete?.Invoke();
            WorldUIManager.Instance.Return(this);
        });
    }

    public void Pause()  => _sequence?.Pause();
    public void Resume() => _sequence?.Play();

    public void Kill()
    {
        _sequence?.Kill();
        WorldUIManager.Instance.Return(this);
    }

    // ── Play (вызывается билдером) ───────────────────────────

    internal void Play(WorldUITween config)
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = config.StartAlpha;

        if (config.StartOffset != Vector3.zero)
        {
            _tracker?.SetUIOffset((Vector2)config.StartOffset);
        }
        else
            _tracker?.SetUIOffset(Vector2.zero);
        
        
        _sequence?.Kill();
        _sequence = DOTween.Sequence();

        if (config.DelayVal > 0)
            _sequence.AppendInterval(config.DelayVal);

        if (config.MoveOffset != Vector3.zero)
        {
            if (_tracker != null)
            {
                Vector2 startOffset = (Vector2)config.StartOffset;
                Vector2 endOffset   = startOffset + (Vector2)config.MoveOffset;

                _tracker.SetUIOffset(startOffset);

                _sequence.Join(
                    _tracker.TweenUIOffset(endOffset, config.DurationVal)
                );
            }
            else
            {
                Vector2 endPos = _rect.anchoredPosition + (Vector2)config.MoveOffset;
                _sequence.Join(
                    _rect.DOAnchorPos(endPos, config.DurationVal).SetEase(Ease.OutCubic)
                );
            }
        }

        if (config.FadeInVal > 0)
            _sequence.Join(canvasGroup.DOFade(1f, config.FadeInVal));

        if (config.ScalePunch)
            _sequence.Join(_rect.DOPunchScale(Vector3.one * 0.3f, 0.4f, 5, 0.5f));

        if (config.FadeOutVal > 0)
        {
            float fadeOutStart = config.DurationVal - config.FadeOutVal;
            _sequence.Insert(
                config.DelayVal + Mathf.Max(0, fadeOutStart),
                canvasGroup.DOFade(0f, config.FadeOutVal)
            );
        }

        _sequence.AppendInterval(config.DurationVal);

        _sequence.OnComplete(() =>
        {
            config.OnCompleteCallback?.Invoke();
            if (config.AutoReturn)
                WorldUIManager.Instance.Return(this);
        });
    }
}