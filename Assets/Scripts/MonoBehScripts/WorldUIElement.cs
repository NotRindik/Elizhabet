using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class WorldUIElement : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    private RectTransform _visualRect; 

    private WorldUITracker _tracker;
    private Sequence _sequence;
    private RectTransform _rect;
    
    [SerializeReference] private List<IWorldElementModule> modules = new(); // нужен using System.Collections.Generic
    private Dictionary<Type, IWorldElementModule> _moduleMap;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _tracker = GetComponent<WorldUITracker>();
        _visualRect = canvasGroup.GetComponent<RectTransform>();

        _moduleMap = new Dictionary<Type, IWorldElementModule>();
        foreach (var module in modules)
        foreach (var iface in module.GetType().GetInterfaces())
            if (iface != typeof(IWorldElementModule) && typeof(IWorldElementModule).IsAssignableFrom(iface))
                _moduleMap[iface] = module;
    }

    private T GetModule<T>() where T : class, IWorldElementModule
    {
        if (_moduleMap.TryGetValue(typeof(T), out var module))
            return module as T;

        Debug.LogWarning($"[{name}] нет модуля {typeof(T).Name} — операция пропущена.", this);
        return null;
    }
    
    
    public WorldUIElement SetSprite(Sprite sprite)
    {
        GetModule<ISpriteModule>()?.SetSprite(sprite);
        return this;
    }
    public WorldUIElement SetNativeSize()
    {
        GetModule<ISpriteModule>()?.SetNativeSize();
        return this;
    }

    public WorldUIElement SetSpriteColor(Color color)
    {
        GetModule<ISpriteModule>()?.SetColor(color);
        return this;
    }
    
    // ── Позиционирование ────────────────────────────────────

    public WorldUIElement SetText(string text)
    {
        GetModule<ITextModule>()?.SetText(text);
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
        GetModule<ITextModule>()?.SetFontSize(size);
        return this;
    }
    
    public WorldUIElement SetFont(string name)
    {
        GetModule<ITextModule>()?.SetFont(name);
        return this;
    }

    public WorldUIElement SetColor(Color color)
    {
        GetModule<ITextModule>()?.SetColor(color);
        return this;
    }


    public WorldUIElement SetAlignment(TextAlignmentOptions alignment)
    {
        GetModule<ITextModule>()?.SetAlignment(alignment);
        return this;
    }

    public WorldUIElement SetBold(bool bold)
    {
        GetModule<ITextModule>()?.SetBold(bold);
        return this;
    }

    public WorldUIElement AppendText(string text)
    {
        GetModule<ITextModule>()?.AppendText(text);
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
        GetModule<ITextModule>()?.TweenFontSize(to,duration,ease);
        return this;
    }

    public WorldUIElement TweenColor(Color to, float duration, Ease ease = Ease.OutCubic)
    {
        GetModule<ITextModule>()?.TweenColor(to,duration,ease);
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

    _visualRect.localScale = Vector3.one * config.StartScale;
    _visualRect.localRotation = Quaternion.identity;
    _visualRect.anchoredPosition = Vector2.zero;

    // Стартовый поворот
    if (config.RandomRotation)
        _visualRect.localRotation = Quaternion.Euler(
            0,
            0,
            Random.Range(-config.RotationRange, config.RotationRange));
    else
        _visualRect.localRotation = Quaternion.Euler(0, 0, config.Rotation);

    Vector2 startOffset = (Vector2)config.StartOffset;

    if (config.RandomOffsetX)
        startOffset.x += Random.Range(-config.RandomOffsetXRange, config.RandomOffsetXRange);

    _tracker?.SetUIOffset(startOffset);

    _sequence?.Kill();
    _sequence = DOTween.Sequence();

    if (config.DelayVal > 0)
        _sequence.AppendInterval(config.DelayVal);

    // Движение по миру (Tracker)
    if (config.MoveOffset != Vector3.zero)
    {
        if (_tracker != null)
        {
            Vector2 endOffset = startOffset + (Vector2)config.MoveOffset;

            _tracker.SetUIOffset(startOffset);

            _sequence.Join(
                _tracker.TweenUIOffset(endOffset, config.DurationVal)
                    .SetEase(config.MoveEase));
        }
        else
        {
            Vector2 endPos = _rect.anchoredPosition + startOffset + (Vector2)config.MoveOffset;

            _rect.anchoredPosition += startOffset;

            _sequence.Join(
                _rect.DOAnchorPos(endPos, config.DurationVal)
                    .SetEase(config.MoveEase));
        }
    }

    // Fade
    if (config.FadeInVal > 0)
        _sequence.Join(canvasGroup.DOFade(1f, config.FadeInVal));

    // Scale
    if (!Mathf.Approximately(config.StartScale, config.EndScale))
    {
        _sequence.Join(
            _visualRect.DOScale(config.EndScale, config.DurationVal)
                .SetEase(config.ScaleEase));
    }

    // Rotation
    if (config.RotateOverTime)
    {
        _sequence.Join(
            _visualRect.DORotate(
                new Vector3(0, 0, config.EndRotation),
                config.DurationVal));
    }

    // Pop
    if (config.Pop)
    {
        _sequence.Join(
            _visualRect.DOScale(config.PopScale, config.PopDuration)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(config.ScaleEase));
    }

    // Punch
    if (config.ScalePunch)
    {
        _sequence.Join(
            _visualRect.DOPunchScale(Vector3.one * 0.3f, 0.4f, 5, 0.5f));
    }

    // Shake
    if (config.Shake)
    {
        _sequence.Join(
            _visualRect.DOShakeAnchorPos(
                config.ShakeDuration,
                config.ShakeStrength));
    }

    // Fade Out
    if (config.FadeOutVal > 0)
    {
        float fadeOutStart = config.DurationVal - config.FadeOutVal;

        _sequence.Insert(
            config.DelayVal + Mathf.Max(0, fadeOutStart),
            canvasGroup
                .DOFade(0f, config.FadeOutVal)
                .SetEase(config.FadeEase));
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

public interface IWorldElementModule
{
    void ResetState();
}

public interface ITextModule : IWorldElementModule
{
    void SetText(string text);
    void AppendText(string text);
    void SetFont(string fontName);
    void SetFontSize(float size);
    void SetColor(Color color);
    void SetAlignment(TextAlignmentOptions alignment);
    void SetBold(bool bold);
    void TweenFontSize(float to, float duration, Ease ease);
    void TweenColor(Color to, float duration, Ease ease);
}

public interface ISpriteModule : IWorldElementModule
{
    void SetSprite(Sprite sprite);
    void SetColor(Color color);
    void TweenColor(Color to, float duration, Ease ease);

    void SetNativeSize();
}

[Serializable]
public class TextModule : ITextModule
{
    [SerializeField] private TextMeshProUGUI label;

    private Tweener _colorTween;
    private Tweener _fontSizeTween;

    public void SetText(string text) => label.text = text;
    public void AppendText(string text) => label.text += text;
    public void SetFontSize(float size) => label.fontSize = size;
    public void SetColor(Color color) => label.color = color;
    public void SetAlignment(TextAlignmentOptions alignment) => label.alignment = alignment;

    public void SetBold(bool bold) => label.fontStyle = bold
        ? label.fontStyle | FontStyles.Bold
        : label.fontStyle & ~FontStyles.Bold;

    public void SetFont(string fontName)
    {
        if (!WorldUIManager.Instance.fonts.TryGetValue(fontName, out var font))
        {
            Debug.LogWarning($"Font '{fontName}' не найден", label);
            return;
        }
        label.font = font;
    }

    public void TweenFontSize(float to, float duration, Ease ease)
    {
        _fontSizeTween?.Kill();
        _fontSizeTween = DOTween.To(() => label.fontSize, x => label.fontSize = x, to, duration).SetEase(ease);
    }

    public void TweenColor(Color to, float duration, Ease ease)
    {
        _colorTween?.Kill();
        _colorTween = label.DOColor(to, duration).SetEase(ease);
    }

    public void ResetState()
    {
        _colorTween?.Kill();
        _fontSizeTween?.Kill();
        label.color = Color.white;
    }
}

[Serializable]
public class SpriteModule : ISpriteModule
{
    [SerializeField] private Image icon;

    private Tweener _colorTween;

    public void SetSprite(Sprite sprite) => icon.sprite = sprite;
    public void SetColor(Color color) => icon.color = color;

    public void TweenColor(Color to, float duration, Ease ease)
    {
        _colorTween?.Kill();
        _colorTween = icon.DOColor(to, duration).SetEase(ease);
    }
    public void SetNativeSize()
    {
        icon.SetNativeSize();
    }

    public void ResetState()
    {
        _colorTween?.Kill();
        icon.color = Color.white;
    }
}