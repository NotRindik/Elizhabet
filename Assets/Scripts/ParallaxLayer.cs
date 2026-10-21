
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class ParallaxLayer : MonoBehaviour
{
    [SerializeField] Transform target;

    [Header("Parallax Strength")]
    [Range(-1f, 1f)] public float parallaxX = 0.5f;
    [Range(-1f, 1f)] public float parallaxY = 0.5f;

    public Vector2 ofset;
    public Vector3 _startPos;

    Vector3 _startTargetPos;
    Vector3 targetPos;

    bool _wasPlaying;
    public bool _initialized;

    void OnEnable()
    {
        _wasPlaying = Application.isPlaying;
        InitializeTarget();
    }

    void InitializeTarget()
    {
        target = GetTargetCamera();

        if (target == null)
            return;

        if (!_initialized)
        {
            _startPos = transform.position;
            _initialized = true;
        }

        _startTargetPos = target.position;
    }

    Transform GetTargetCamera()
    {
        if (Application.isPlaying)
            return Camera.main != null
                ? Camera.main.transform
                : null;

#if UNITY_EDITOR
        if (!Application.isPlaying &&
            SceneView.lastActiveSceneView != null)
        {
            Camera sceneCamera =
                SceneView.lastActiveSceneView.camera;

            if (sceneCamera != null)
                return sceneCamera.transform;
        }
#endif

        return null;
    }

    void LateUpdate()
    {
        if (_wasPlaying != Application.isPlaying)
        {
            _wasPlaying = Application.isPlaying;
            InitializeTarget();
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Transform editorTarget = GetTargetCamera();

            if (editorTarget != target)
            {
                target = editorTarget;

                if (target != null)
                    _startTargetPos = target.position;
            }
        }
#endif

        if (target == null)
            return;

        Vector3 delta = target.position - _startTargetPos;

        targetPos = _startPos;
        targetPos.x += delta.x * parallaxX;
        targetPos.y += delta.y * parallaxY;

        transform.position = targetPos + (Vector3)ofset;
    }
}
