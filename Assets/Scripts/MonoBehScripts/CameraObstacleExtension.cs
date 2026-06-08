using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

[ExecuteAlways]
[SaveDuringPlay]
[AddComponentMenu("")]
public class CameraObstacleExtension : MonoBehaviour, ICameraExtension
{
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector2 viewportPadding = new(0.2f, 0.2f);

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color freeColor = Color.green;
    [SerializeField] private Color blockedColor = Color.red;

    public bool TurnPause;

    private class GateState
    {
        public float activeSide;
        public bool isCrossed;
        public bool isActive = true;
        public Vector3 smoothVelocity;
        public Vector3 releaseTarget;
        public float lerpTime;
        public Vector3 releaseStart;
    }

    private readonly Dictionary<CameraObstacle, GateState> _gateStates = new();
    private bool _blocked;
    private Vector2 _debugSize;
    private Vector3 _currentPosition;

    public CinemachineVirtualCamera VirtualCamera;
    public int priority;
    public int Priority => priority;

    private void Start()
    {
        VirtualCamera = GetComponent<CinemachineVirtualCamera>();
    }

    public void Execute(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if(!Application.isPlaying)
            return;
        
        if (stage != CinemachineCore.Stage.Body) return;
        if (player == null || !VirtualCamera) return;

        Vector3 desiredPos = state.RawPosition;
        Vector2 castSize = GetCameraWorldSize(vcam) - viewportPadding;
        _debugSize = castSize;
        _blocked = false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(desiredPos, castSize, 0f, obstacleMask);
        var obstacles = CollectObstacles(hits, player.position);

        if (obstacles.Count == 0)
        {
            _gateStates.Clear();
            SetCameraPosition(desiredPos, ref state);
            return;
        }

        CleanupInactiveGates(obstacles);

        Vector3 resultPos = desiredPos;

        foreach (var obstacle in obstacles)
        {
            if (!_gateStates.TryGetValue(obstacle, out GateState gs))
            {
                gs = new GateState();
                _gateStates[obstacle] = gs;
            }

            switch (obstacle.Type)
            {
                case CameraObstacleType.Hard:
                    resultPos = ApplyHard(obstacle, resultPos);
                    break;
                case CameraObstacleType.OneWay:
                    resultPos = ApplyOneWay(obstacle, gs, resultPos, deltaTime);
                    break;
                case CameraObstacleType.TwoWay:
                    resultPos = ApplyTwoWay(obstacle, gs, resultPos, deltaTime);
                    break;
            }
        }

        SetCameraPosition(resultPos, ref state);
    }

    private List<CameraObstacle> CollectObstacles(Collider2D[] hits, Vector3 playerPos)
    {
        var result = new List<CameraObstacle>(hits.Length);
        foreach (var hit in hits)
        {
            var obstacle = hit.GetComponent<CameraObstacle>();
            if (obstacle == null) continue;
            switch (obstacle.Type)
            {
                case CameraObstacleType.Hard:
                case CameraObstacleType.OneWay:
                case CameraObstacleType.TwoWay:
                    result.Add(obstacle);
                    break;
            }
        }
        return result;
    }

    private void CleanupInactiveGates(List<CameraObstacle> active)
    {
        var toRemove = new List<CameraObstacle>();
        foreach (var key in _gateStates.Keys)
            if (!active.Contains(key))
                toRemove.Add(key);
        foreach (var key in toRemove)
            _gateStates.Remove(key);
    }

    private Vector3 ApplyHard(CameraObstacle obstacle, Vector3 desiredPos)
    {
        _blocked = true;
        Bounds obs = obstacle.Collider.bounds;
        Bounds cam = GetCameraBounds(desiredPos);
        if (!cam.Intersects(obs)) return desiredPos;
        return ClampCameraToObstacle(desiredPos, cam, obs);
    }

    private Vector3 ApplyOneWay(CameraObstacle obstacle, GateState gs, Vector3 desiredPos, float deltaTime)
    {
        Vector2 toPlayer = (Vector2)(player.position - obstacle.transform.position);
        float dot = Vector2.Dot(obstacle.Normal, toPlayer);
        float currentSide = Mathf.Sign(dot);

        if (!gs.isActive)
            return desiredPos;

        if (gs.activeSide == 0f)
        {
            if (dot <= 0f) return desiredPos;

            Vector2 toCamera0 = (Vector2)(_currentPosition - obstacle.transform.position);
            float camDot0 = Vector2.Dot(obstacle.Normal, toCamera0);
            if (camDot0 <= 0f)
            {
                gs.isActive = false;
                return desiredPos;
            }

            gs.activeSide = currentSide;
            gs.smoothVelocity = Vector3.zero;
        }

// камера с другой стороны — сбрасываем всё
        Vector2 toCamera = (Vector2)(_currentPosition - obstacle.transform.position);
        float camDot = Vector2.Dot(obstacle.Normal, toCamera);
        if (Mathf.Sign(camDot) != gs.activeSide)
        {
            Debug.Log("Abiba");
            gs.isActive = false;
        }

        if (!gs.isCrossed)
        {
            bool insideCollider = obstacle.Collider.OverlapPoint(player.position);
            bool stillOnActiveSide = currentSide == gs.activeSide;
            
            bool cameraOnWrongSide = Mathf.Sign(camDot) != gs.activeSide;

            // камера перелетела но игрок не переходил — не блокируем
            if (cameraOnWrongSide && stillOnActiveSide)
                return desiredPos;

            if (insideCollider || stillOnActiveSide)
                return ApplyBlock(obstacle, gs, desiredPos, deltaTime);

            gs.isCrossed = true;
            gs.lerpTime = 0f;
            gs.smoothVelocity = Vector3.zero;
        }

        // игрок перешёл — двигаем камеру к игроку за фиксированное время
        if (gs.isCrossed)
        {
            gs.lerpTime += deltaTime;
            float t = Mathf.Clamp01(gs.lerpTime / obstacle.ReleaseTime);
            _currentPosition = Vector3.Lerp(_currentPosition, desiredPos, t);

            if (t >= 1f)
            {
                _currentPosition = desiredPos;
                gs.isCrossed = false;
                gs.activeSide = 0f;
                gs.isActive = false;
            }

            return _currentPosition;
        }

        return desiredPos;
    }

    private Vector3 ApplyTwoWay(CameraObstacle obstacle, GateState gs, Vector3 desiredPos, float deltaTime)
    {
        Vector2 toPlayer = (Vector2)(player.position - obstacle.transform.position);
        float dot = Vector2.Dot(obstacle.Normal, toPlayer);
        float currentSide = Mathf.Sign(dot);

        if (gs.activeSide == 0f)
        {
            gs.activeSide = currentSide;
            gs.smoothVelocity = Vector3.zero;
        }

        if (!gs.isCrossed && currentSide != gs.activeSide)
        {
            gs.isCrossed = true;
            gs.lerpTime = 0f;

            gs.releaseStart = _currentPosition;
            gs.releaseTarget = ComputeExitTarget(obstacle, desiredPos);
        }

        if (gs.isCrossed)
        {
            // игрок вернулся назад во время перехода — отменяем
            if (currentSide == gs.activeSide)
            {
                float normalized =
                    Mathf.Clamp01(gs.lerpTime / obstacle.ReleaseTime);

                float speedMultiplier =
                    Mathf.Lerp(0.45f, 1f, normalized);

                gs.lerpTime -= deltaTime * speedMultiplier;
            }
            else
            {
                gs.lerpTime += deltaTime;   
            }
            
            float t = Mathf.Clamp01(gs.lerpTime / obstacle.ReleaseTime);

            _currentPosition = Vector3.Lerp(
                gs.releaseStart,
                gs.releaseTarget,
                t
            );
            
            _currentPosition = Vector3.Lerp(_currentPosition, gs.releaseTarget, t);

            if (t >= 1f)
            {
                _currentPosition = gs.releaseTarget;
                gs.isCrossed = false;
                gs.activeSide = currentSide;
            }
            
            if (t <= 0f)
            {
                gs.isCrossed = false;
                gs.lerpTime = 0f;
            }

            return _currentPosition;
        }

        return ApplyBlock(obstacle, gs, desiredPos, deltaTime);
    }

    private Vector3 ApplyBlock(CameraObstacle obstacle, GateState gs, Vector3 desiredPos, float deltaTime)
    {
        _blocked = true;

        Bounds obs = obstacle.Collider.bounds;
        float halfW = GetCameraWorldSize(VirtualCamera).x * 0.5f;
        float halfH = GetCameraWorldSize(VirtualCamera).y * 0.5f;

        Vector3 curDelta = new Vector3(_currentPosition.x, _currentPosition.y, 0f) - obs.center;
        float curOvX = obs.extents.x + halfW - Mathf.Abs(curDelta.x);
        float curOvY = obs.extents.y + halfH - Mathf.Abs(curDelta.y);

        if (curOvX <= 0f || curOvY <= 0f)
        {
            Bounds cam = GetCameraBounds(desiredPos);
            if (!cam.Intersects(obs)) return desiredPos;
        }

        bool blockedByX = curOvX < curOvY;
        Vector3 sliding;

        if (blockedByX)
        {
            float clampedX = curDelta.x > 0f ? obs.max.x + halfW : obs.min.x - halfW;
            sliding = new Vector3(clampedX, desiredPos.y, desiredPos.z);
        }
        else
        {
            float clampedY = curDelta.y > 0f ? obs.max.y + halfH : obs.min.y - halfH;
            sliding = new Vector3(desiredPos.x, clampedY, desiredPos.z);
        }

        if (obstacle.Transition == CameraTransitionType.Smooth)
        {
            _currentPosition = Vector3.SmoothDamp(_currentPosition, sliding, ref gs.smoothVelocity, obstacle.ReleaseTime, Mathf.Infinity, deltaTime);
            return _currentPosition;
        }

        return sliding;
    }

    private Vector2 GetCameraWorldSize(CinemachineVirtualCameraBase vcam)
    {
        LensSettings lens = vcam.State.Lens;
        float height = lens.OrthographicSize * 2f;
        float width = height * lens.Aspect;
        return new Vector2(width, height);
    }

    private Bounds GetCameraBounds(Vector3 position)
    {
        Vector2 size = GetCameraWorldSize(VirtualCamera);
        position.z = 0f;
        return new Bounds(position, new Vector3(size.x, size.y, 999f));
    }

    private void SetCameraPosition(Vector3 position, ref CameraState state)
    {
        state.RawPosition = position;
        _currentPosition = position;
    }

    private Vector3 ClampCameraToObstacle(Vector3 desiredPos, Bounds cameraBounds, Bounds obstacleBounds)
    {
        Vector3 corrected = desiredPos;
        float halfW = cameraBounds.extents.x;
        float halfH = cameraBounds.extents.y;

        Vector3 delta = cameraBounds.center - obstacleBounds.center;
        float overlapX = obstacleBounds.extents.x + halfW - Mathf.Abs(delta.x);
        float overlapY = obstacleBounds.extents.y + halfH - Mathf.Abs(delta.y);

        if (overlapX <= 0 || overlapY <= 0) return corrected;

        Vector3 curDelta = new Vector3(_currentPosition.x, _currentPosition.y, 0f) - obstacleBounds.center;
        float curOvX = obstacleBounds.extents.x + halfW - Mathf.Abs(curDelta.x);
        float curOvY = obstacleBounds.extents.y + halfH - Mathf.Abs(curDelta.y);

        bool clampX = (curOvX > 0f && curOvY > 0f) ? curOvX < curOvY : overlapX < overlapY;

        if (clampX)
            corrected.x = delta.x > 0f ? obstacleBounds.max.x + halfW : obstacleBounds.min.x - halfW;
        else
            corrected.y = delta.y > 0f ? obstacleBounds.max.y + halfH : obstacleBounds.min.y - halfH;

        return corrected;
    }

    private Vector3 ComputeExitTarget(CameraObstacle obstacle, Vector3 desiredPos)
    {
        Bounds obs = obstacle.Collider.bounds;
        float halfW = GetCameraWorldSize(VirtualCamera).x * 0.5f;
        float halfH = GetCameraWorldSize(VirtualCamera).y * 0.5f;
        Vector3 target = desiredPos;

        Vector2 dir = (Vector2)_currentPosition - (Vector2)obs.center;
        float ovX = obs.extents.x + halfW - Mathf.Abs(dir.x);
        float ovY = obs.extents.y + halfH - Mathf.Abs(dir.y);

        if (ovX < ovY)
            target.x = dir.x > 0f ? obs.min.x - halfW : obs.max.x + halfW;
        else
            target.y = dir.y > 0f ? obs.min.y - halfH : obs.max.y + halfH;

        return target;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = _blocked ? blockedColor : freeColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(_debugSize.x, _debugSize.y, 0.01f));
    }
#endif
}