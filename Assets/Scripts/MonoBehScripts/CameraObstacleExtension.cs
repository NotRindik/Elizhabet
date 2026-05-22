using System;
using Cinemachine;
using UnityEngine;

[ExecuteAlways]
[SaveDuringPlay]
[AddComponentMenu("")]
public class CameraObstacleExtension : MonoBehaviour,ICameraExtension
{
    [SerializeField]
    private Transform player;

    [SerializeField]
    private LayerMask obstacleMask;

    [SerializeField]
    private Vector2 viewportPadding =
        new(0.2f, 0.2f);

    [Header("Debug")]
    [SerializeField]
    private bool drawGizmos = true;

    [SerializeField]
    private Color freeColor =
        Color.green;

    [SerializeField]
    private Color blockedColor =
        Color.red;

    private CameraObstacle _activeGate;
    private bool _blocked;
    private Vector2 _debugSize;

    private Vector3 _smoothVelocity;
    private Vector3 _currentPosition;

    public CinemachineVirtualCamera VirtualCamera;
    
    private Vector3 _releaseTarget; // для TwoWay: позиция выхода через второй край

    private Vector2 GetCameraWorldSize(
        CinemachineVirtualCameraBase vcam)
    {
        LensSettings lens =
            vcam.State.Lens;

        float height =
            lens.OrthographicSize * 2f;

        float width =
            height * lens.Aspect;

        return new Vector2(
            width,
            height);
    }
    public int priority;
    public int Priority => priority;
    
    private float _activeSide;   // сторона игрока при первом касании
    
    private bool _isCrossed;     // игрок уже перешёл через gate

    private void Start()
    {
        VirtualCamera = GetComponent<CinemachineVirtualCamera>();
    }
    public void Execute(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage !=
            CinemachineCore.Stage.Body)
            return;

        if (player == null || !VirtualCamera)
            return;

        Vector3 desiredPos =
            state.RawPosition;

        Vector2 castSize =
            GetCameraWorldSize(vcam)
            - viewportPadding;

        _debugSize = castSize;
        _blocked = false;

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                desiredPos,
                castSize,
                0f,
                obstacleMask
            );

        CameraObstacle blocking =
            FindBlockingObstacle(
                hits,
                player.position);

        // Нет obstacle
        if (blocking == null)
        {
            _activeGate = null;
            _isCrossed  = false;      // ← добавить

            state.RawPosition  = desiredPos;
            _currentPosition   = desiredPos;
            return;
        }
        

        switch (blocking.Type)
        {
            case CameraObstacleType.Hard:
            {
                HandleHardCollide(
                    blocking,
                    desiredPos,
                    ref state,
                    deltaTime);
                break;
            }

            case CameraObstacleType.OneWay:
            {
                HandleOneWay(
                    blocking,
                    desiredPos,
                    ref state,
                    deltaTime);

                break;
            }

            case CameraObstacleType.TwoWay:
            {
                HandleTwoWay(
                    blocking,
                    desiredPos,
                    ref state,
                    deltaTime);

                break;
            }
        }
    }

    private void HandleHardCollide(
        CameraObstacle obstacle,
        Vector3 desiredPos,
        ref CameraState state,
        float deltaTime)
    {
        _blocked = true;

        Bounds obstacleBounds =
            obstacle.Collider.bounds;

        Bounds cameraBounds =
            GetCameraBounds(desiredPos);

        if (!cameraBounds.Intersects(
                obstacleBounds))
        {
            SetCameraPosition(
                desiredPos,
                ref state);

            return;
        }

        Debug.Log("Intersect");

        Vector3 correctedPos =
            ClampCameraToObstacle(
                desiredPos,
                cameraBounds,
                obstacleBounds);

        SetCameraPosition(
            correctedPos,
            ref state);
    }
    
    private void ApplyBlock(
        CameraObstacle obstacle,
        Vector3 desiredPos,
        ref CameraState state,
        float deltaTime)
    {
        _blocked = true;

        Bounds obstacleBounds = obstacle.Collider.bounds;
        Bounds cameraBounds   = GetCameraBounds(desiredPos);

        if (!cameraBounds.Intersects(obstacleBounds))
        {
            SetCameraPosition(desiredPos, ref state);
            return;
        }

        Vector3 corrected = ClampCameraToObstacle(
            desiredPos, cameraBounds, obstacleBounds);

        if (obstacle.Transition == CameraTransitionType.Smooth)
        {
            _currentPosition = Vector3.SmoothDamp(
                _currentPosition,
                corrected,
                ref _smoothVelocity,
                obstacle.SmoothTime,
                Mathf.Infinity,
                deltaTime);

            state.RawPosition = _currentPosition;
        }
        else
        {
            SetCameraPosition(corrected, ref state);
        }
    }
    private Vector3 ComputeExitTarget(
        CameraObstacle obstacle,
        Vector3 desiredPos)
    {
        Bounds obs = obstacle.Collider.bounds;
        float halfW = GetCameraWorldSize(VirtualCamera).x * 0.5f;
        float halfH = GetCameraWorldSize(VirtualCamera).y * 0.5f;

        // Берём желаемую позицию как базу — свободная ось следует за игроком
        Vector3 target = desiredPos;

        Vector2 dir =
            (Vector2)_currentPosition - (Vector2)obs.center;

        float ovX = obs.extents.x + halfW - Mathf.Abs(dir.x);
        float ovY = obs.extents.y + halfH - Mathf.Abs(dir.y);

        if (ovX < ovY) // камера заблокирована по X
        {
            target.x = dir.x > 0f
                ? obs.min.x - halfW  // была справа → выход слева
                : obs.max.x + halfW; // была слева  → выход справа
        }
        else           // камера заблокирована по Y
        {
            target.y = dir.y > 0f
                ? obs.min.y - halfH  // была сверху → выход снизу
                : obs.max.y + halfH; // была снизу  → выход сверху
        }

        return target;
    }
    private Vector3 ClampCameraToObstacle(
        Vector3 desiredPos,
        Bounds cameraBounds,
        Bounds obstacleBounds)
    {
        Vector3 corrected = desiredPos;

        float halfW = cameraBounds.extents.x;
        float halfH = cameraBounds.extents.y;

        Vector3 delta =
            cameraBounds.center - obstacleBounds.center;

        float overlapX =
            obstacleBounds.extents.x + halfW - Mathf.Abs(delta.x);
        float overlapY =
            obstacleBounds.extents.y + halfH - Mathf.Abs(delta.y);

        if (overlapX <= 0 || overlapY <= 0)
            return corrected;

        // Ось определяем по ТЕКУЩЕЙ позиции камеры, а не по desiredPos.
        // _currentPosition уже прижата к грани → ось не флипает,
        // пока камера скользит вдоль препятствия.
        Vector3 curDelta = new Vector3(
            _currentPosition.x,
            _currentPosition.y,
            0f) - obstacleBounds.center;

        float curOverlapX =
            obstacleBounds.extents.x + halfW - Mathf.Abs(curDelta.x);
        float curOverlapY =
            obstacleBounds.extents.y + halfH - Mathf.Abs(curDelta.y);

        // Если текущая позиция уже касается препятствия — берём её ось.
        // Первый кадр контакта (_currentPosition ещё далеко) — fallback на desiredPos.
        bool clampX = (curOverlapX > 0f && curOverlapY > 0f)
            ? curOverlapX < curOverlapY
            : overlapX < overlapY;

        if (clampX)
        {
            bool cameraRight = delta.x > 0f;
            corrected.x = cameraRight
                ? obstacleBounds.max.x + halfW
                : obstacleBounds.min.x - halfW;
        }
        else
        {
            bool cameraTop = delta.y > 0f;
            corrected.y = cameraTop
                ? obstacleBounds.max.y + halfH
                : obstacleBounds.min.y - halfH;
        }

        return corrected;
    }
    
    private Bounds GetCameraBounds(
        Vector3 position)
    {
        Vector2 cameraSize =
            GetCameraWorldSize(
                VirtualCamera);

        position.z = 0f;

        return new Bounds(
            position,
            new Vector3(
                cameraSize.x,
                cameraSize.y,
                999f));
    }
    
    private void SetCameraPosition(
        Vector3 position,
        ref CameraState state)
    {
        state.RawPosition =
            position;

        _currentPosition =
            position;
    }

    private CameraObstacle FindBlockingObstacle(
        Collider2D[] hits,
        Vector3 playerPos)
    {
        foreach (var hit in hits)
        {
            CameraObstacle obstacle =
                hit.GetComponent<CameraObstacle>();

            if (obstacle == null)
                continue;

            switch (obstacle.Type)
            {
                case CameraObstacleType.Hard:
                case CameraObstacleType.OneWay:
                case CameraObstacleType.TwoWay:
                    return obstacle;
            }
        }

        return null;
    }

    private void HandleOneWay(
        CameraObstacle obstacle,
        Vector3 desiredPos,
        ref CameraState state,
        float deltaTime)
    {
        // Игрок физически внутри коллайдера — не блокируем (фикс бага)
        if (obstacle.Collider.bounds.Contains(player.position))
        {
            SetCameraPosition(desiredPos, ref state);
            return;
        }

        Vector2 toPlayer =
            (Vector2)(player.position - obstacle.transform.position);

        float dot         = Vector2.Dot(obstacle.Normal, toPlayer);
        float currentSide = Mathf.Sign(dot);

        // Первый контакт с этим gate
        if (_activeGate != obstacle)
        {
            if (dot <= 0f)
            {
                SetCameraPosition(desiredPos, ref state);
                return;
            }

            _activeGate     = obstacle;
            _activeSide     = currentSide;
            _isCrossed      = false;
            _smoothVelocity = Vector3.zero; // ← сброс при re-entry
        }
        
        if (!_isCrossed && currentSide != _activeSide)
        {
            _isCrossed = true;
            _smoothVelocity = Vector3.zero;
        }

        if (_isCrossed)
        {
            if (obstacle.Transition == CameraTransitionType.Smooth)
            {
                _currentPosition = Vector3.SmoothDamp(
                    _currentPosition,
                    desiredPos,
                    ref _smoothVelocity,
                    obstacle.ReleaseTime,
                    Mathf.Infinity,
                    deltaTime);

                state.RawPosition = _currentPosition;
            }
            else
            {
                SetCameraPosition(desiredPos, ref state);
            }
            return;
        }

        ApplyBlock(obstacle, desiredPos, ref state, deltaTime);
    }
    

    private void HandleTwoWay(
        CameraObstacle obstacle,
        Vector3 desiredPos,
        ref CameraState state,
        float deltaTime)
    {
        if (obstacle.Collider.bounds.Contains(player.position))
        {
            SetCameraPosition(desiredPos, ref state);
            return;
        }

        Vector2 toPlayer  = (Vector2)(player.position - obstacle.transform.position);
        float dot         = Vector2.Dot(obstacle.Normal, toPlayer);
        float currentSide = Mathf.Sign(dot);

        if (_activeGate != obstacle)
        {
            _activeGate     = obstacle;
            _activeSide     = currentSide;
            _isCrossed      = false;
            _smoothVelocity = Vector3.zero; // сброс при re-entry → плавный вход в ApplyBlock
        }

        if (!_isCrossed && currentSide != _activeSide)
        {
            _isCrossed      = true;
            _smoothVelocity = Vector3.zero;
            _releaseTarget  = ComputeExitTarget(obstacle, desiredPos); // второй край
        }

        if (_isCrossed)
        {
            if (obstacle.Transition == CameraTransitionType.Smooth)
            {
                _currentPosition = Vector3.SmoothDamp(
                    _currentPosition,
                    _releaseTarget,
                    ref _smoothVelocity,
                    obstacle.SmoothTime,
                    Mathf.Infinity,
                    deltaTime);

                state.RawPosition = _currentPosition;
            }
            else
            {
                SetCameraPosition(_releaseTarget, ref state);
            }
            return;
        }

        ApplyBlock(obstacle, desiredPos, ref state, deltaTime);
    }
    
#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        Gizmos.color =
            _blocked
                ? blockedColor
                : freeColor;

        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(
                _debugSize.x,
                _debugSize.y,
                0.01f));
    }

#endif
}