using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldUITracker : MonoBehaviour
{
    private Transform _target;
    private Vector3 _worldOffset;
    private bool _useStaticPosition;
    private Vector3 _staticWorldPos;

    private RectTransform _rect;
    public RectTransform _canvasRect;
    private Camera _cam;
    
    private Vector2 _uiOffset;

    public void SetUIOffset(Vector2 offset) => _uiOffset = offset;


    private Tween _offsetTween;
    public Tween TweenUIOffset(Vector2 to, float duration, Ease ease = Ease.OutCubic)
    {
        
        _offsetTween?.Kill();
        _offsetTween =DOTween.To(
            () => _uiOffset,
            v  => _uiOffset = v,
            to,
            duration
        ).SetEase(ease);
        
        return _offsetTween;
    }

    private void Start()
    {
        _rect = GetComponent<RectTransform>();
        _cam = ContextManager.Instance.mainCamera;
    }
    
    
    private void LateUpdate()
    {
        UpdatePosition();
    }

    public void SetTarget(Transform target, Vector3 offset = default)
    {
        _target = target;
        _worldOffset = offset;
        _useStaticPosition = false;
    }

    public void SetStaticPosition(Vector3 worldPos)
    {
        _staticWorldPos = worldPos;
        _useStaticPosition = true;
        _target = null;
    }

    private void UpdatePosition()
    {
        if (_rect == null || _canvasRect == null) return;

        Vector3 worldPos;

        if (_useStaticPosition)
            worldPos = _staticWorldPos;
        else if (_target != null)
            worldPos = _target.position + _worldOffset;
        else
            return;

        Vector3 viewportPos = _cam.WorldToViewportPoint(worldPos);

        if (viewportPos.z < 0)
        {
            _rect.anchoredPosition = new Vector2(-99999, -99999);
            return;
        }

        Vector2 canvasSize = _canvasRect.sizeDelta;
        float x = (viewportPos.x - 0.5f) * canvasSize.x;
        float y = (viewportPos.y - 0.5f) * canvasSize.y;

        _rect.anchoredPosition = new Vector2(x, y) + _uiOffset;
    }
}