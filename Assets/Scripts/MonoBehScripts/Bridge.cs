using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

[RequireComponent(typeof(BoxCollider2D))]
public class Bridge : SerializedMonoBehaviour
{
    [Header("Bridge Points")]
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;

    [Header("Animation")]
    [SerializeField] private float _deployDuration = 0.8f;

    [SerializeField] private float _delay = 0;
    [SerializeField] private Ease _deployEase = Ease.OutCubic;

    [SerializeField] SpriteRenderer _renderer;
    private BoxCollider2D _collider;

    private bool _isDeployed;
    private bool _isAnimating;

    private const string STATE_KEY = "deployed";

    public BetterEvent OnStart;
    public BetterEvent OnEnd;

    private WorldObjectsStateSave WorldSave =>
        SaveManager.Instance.GetModule<WorldObjectsStateSave>();

    private string SaveKey => WorldKeyBuilder.Build(this, STATE_KEY);

    private float BridgeLength =>
        Vector2.Distance(_startPoint.position, _endPoint.position);

    private float BridgeAngle
    {
        get
        {
            Vector2 dir = _endPoint.position - _startPoint.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
    }

    private Vector2 GetCenter(float width)
    {
        Vector2 dir = ((Vector2)_endPoint.position - (Vector2)_startPoint.position).normalized;
        return (Vector2)_startPoint.position + dir * (width * 0.5f);
    }

    private void Awake()
    {
        _collider = _renderer.GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        // Читаем состояние из WorldObjectsStateSave, дефолт — сложен
        if (WorldSave.Exist(SaveKey))
            _isDeployed = WorldSave.GetData(SaveKey) == "1";
        else
            _isDeployed = false;

        _renderer.transform.rotation = Quaternion.Euler(0, 0, BridgeAngle);
        ApplyState(instant: true);
    }

    public void Toggle()
    {
        if (_isAnimating) return;
        SetDeployed(!_isDeployed);
    }

    public void Deploy()  => SetDeployed(true);
    public void Retract() => SetDeployed(false);

    public void SetDeployed(bool deployed)
    {
        if (_isDeployed == deployed || _isAnimating) return;

        _isDeployed = deployed;

        // Сохраняем через WorldObjectsStateSave
        WorldSave.SetData(SaveKey, _isDeployed ? "1" : "0");
        SaveManager.Instance.SaveModule<WorldObjectsStateSave>();

        ApplyState(instant: false);
    }

    private void ApplyState(bool instant)
    {
        float targetWidth = _isDeployed ? BridgeLength : 0f;

        _renderer.transform.rotation = Quaternion.Euler(0, 0, BridgeAngle);

        if (instant)
        {
            SetWidth(targetWidth);
            return;
        }

        float currentWidth = _renderer.size.x;
        _isAnimating = true;
        
        DOTween.To(
            () => currentWidth,
            w => 
            { 
                currentWidth = w; 
                SetWidth(w); 
            },
            targetWidth,
            _deployDuration
        ).SetDelay(_delay).SetEase(_deployEase).OnStart(() => OnStart.Invoke()).OnComplete(() =>
            {
                _isAnimating = false;
                OnEnd.Invoke();
            }
        );
    }

    private void SetWidth(float width)
    {
        _renderer.transform.position = GetCenter(width);
        _renderer.size = new Vector2(width, _renderer.size.y);
        _collider.size = _renderer.size;
        _collider.offset = Vector2.zero;
        _collider.enabled = width > 0.01f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_startPoint == null || _endPoint == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_startPoint.position, _endPoint.position);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_startPoint.position, 0.15f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_endPoint.position, 0.15f);

        Vector3 mid = (_startPoint.position + _endPoint.position) * 0.5f;
        float len = Vector2.Distance(_startPoint.position, _endPoint.position);

        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(mid + Vector3.up * 0.25f, $"Bridge length: {len:F2}u");

        // Превью прямоугольника моста
        Vector2 dir  = ((Vector2)_endPoint.position - (Vector2)_startPoint.position).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * 0.5f;

        Vector3 d = new Vector3(dir.x,  dir.y)  * len * 0.5f;
        Vector3 p = new Vector3(perp.x, perp.y);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Vector3[] corners = {
            mid - d - p, mid + d - p,
            mid + d + p, mid - d + p
        };
        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
    }
#endif
}