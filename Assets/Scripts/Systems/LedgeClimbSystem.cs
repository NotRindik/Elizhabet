using System;
using System.Collections;
using Assets.Scripts;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{ 
    public class LedgeClimbSystem : BaseSystem,IStopCoroutineSafely,IDisposable
{
    private ColorPositioningComponent _colorPositioning;
    private WallEdgeClimbComponent _edgeClimb;
    private MoveComponent _moveComponent;
    private AnimationComponent _animationComponent;
    private FSMSystem _fsm;
    private FsmComponent _fsmComponent;
    private Action<bool> jumpHandle;
    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        _moveComponent = owner.GetControllerComponent<MoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
        _edgeClimb = owner.GetControllerComponent<WallEdgeClimbComponent>();
        _fsmComponent = owner.GetControllerComponent<FsmComponent>();
        _fsm = owner.GetControllerSystem<FSMSystem>();
        jumpHandle = c =>
        {
            if (_edgeClimb.EdgeStuckProcess != null)
            {
                StopCoroutineSafely();
                _fsm.SetState(new JumpState((PlayerController)owner));
            }
        };
        owner.GetControllerSystem<IInputProvider>().GetState().Jump.started += jumpHandle;

        owner.OnGizmosUpdate += OnDrawGizmos;
    }

    public override void OnUpdate()
    {
        if (_edgeClimb.EdgeStuckProcess == null)
            _edgeClimb.EdgeStuckProcess = owner.StartCoroutine(EdgeStuckProcess());
    }

    private IEnumerator EdgeStuckProcess()
    {
        if (!CanGrabLedge(out var foreHeadHit, out var tazHit))
        {
            _edgeClimb.EdgeStuckProcess = null;
            yield break;
        }
        var rb = ((EntityController)owner).baseFields.rb;
        
        bool isStick = TryStickToLedge(tazHit, out var floorHit);
        if (!floorHit || !isStick)
        {
            _edgeClimb.EdgeStuckProcess = null;
            yield break;
        }
        _animationComponent.CrossFade("WallEdgeClimb",0.1f);
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0;
        rb.bodyType = RigidbodyType2D.Kinematic;
        _edgeClimb.SaveTemp();
        _edgeClimb.floorCheckPosFromPlayer = 0.08f;
        _edgeClimb.foreHeadRayDistance = 0.39f/2;
        int flip = (int)owner.transform.localScale.x;
        bool isClimb = false;
        while (_colorPositioning.spriteRenderer.sprite != _edgeClimb.waitSprite)
        { 
            yield return null;
        }
        while (true)
        {
            yield return null;
            var headClear = !Physics2D.Raycast(ForeHeadCheckPos() , Vector2.up, _edgeClimb.heightHeadRayDistance, _edgeClimb.wallLayerMask);

            CanGrabLedge(out foreHeadHit, out tazHit);
            if (headClear && !foreHeadHit)
            {
                if (_moveComponent.direction.x == flip )
                {
                    yield return new WaitForSeconds(0.2f);
                    isClimb = true;
                    break;
                }
            }
            if (_moveComponent.direction.x != flip && _moveComponent.direction.x != 0)
            {
                yield return new WaitForSeconds(0.1f);

                break;
            }
            var rot = _colorPositioning.spriteRenderer.transform.eulerAngles;
            float period = 2f;
            float amplitude = -2.3f;

            float angle = Mathf.Sin(Time.time * Mathf.PI * 2f / period) * amplitude;
            rot.z = angle;
            _colorPositioning.spriteRenderer.transform.rotation = Quaternion.Euler(rot);
            floorHit = Physics2D.Raycast(
                ForeHeadCheckPos(),
                Vector2.down,
                _edgeClimb.floorCheckDistance,
                _edgeClimb.wallLayerMask
            );
        }
        TeleportToClimbPosition(floorHit,isClimb);
        ResetPlayerPhysics();
        _edgeClimb.Reset();
        _edgeClimb.EdgeStuckProcess = null;
    }
    private void TeleportToClimbPosition(RaycastHit2D floor , bool isClimb)
    {
        if (floor.collider)
        {
            if (isClimb)
            {
                owner.transform.position = floor.point + Vector2.up * 0.8f;
                return;
            }
        }
        owner.transform.position +=  new Vector3(Vector2.right.x * 0.5f * -owner.transform.localScale.x,-0.5f);
    }

    private void ResetPlayerPhysics()
    {
        var rb = ((EntityController)owner).baseFields.rb;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1;
    }

    private bool TryStickToLedge(RaycastHit2D tazHit, out RaycastHit2D floorHit)
    {
        floorHit = Physics2D.Raycast(ForeHeadCheckPos(), Vector2.down, _edgeClimb.floorCheckDistance, _edgeClimb.wallLayerMask);
        float delta = 0.2f; // допустимое отклонение

        float floorY = floorHit.point.y;
        float pelvisY = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint().y;

        bool isWithinRange = Mathf.Abs(floorY - pelvisY) <= delta;
        
        if (!floorHit.collider || Vector3.Dot(floorHit.normal, Vector3.up) < 0.5f || !isWithinRange)
            return false;

        var newX = tazHit.point.x - owner.transform.right.x * 0.1f * owner.transform.localScale.x;
        var newY = floorHit.point.y + 0.41f;
        owner.transform.position = new Vector2(newX, newY);
        return true;
    }

    private Vector2 ForeHeadCheckPos() =>
        _colorPositioning.pointsGroup[ColorPosNameConst.BOOBS].FirstActivePoint()
        + new Vector2(_edgeClimb.floorCheckPosFromPlayer, 0) * owner.transform.localScale.x;

    public bool CanGrabLedge(out RaycastHit2D foreHeadHit, out RaycastHit2D tazHit)
    {
        Vector2 dir = owner.transform.right * owner.transform.localScale.x;
        if (_animationComponent.currentState == "VerticalWallRun")
        {
            Vector2 hand = _colorPositioning.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
            Vector2 hand2 = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();

            foreHeadHit = Physics2D.Raycast(hand, dir, _edgeClimb.foreHeadRayDistance, _edgeClimb.wallLayerMask);
            tazHit = Physics2D.Raycast(hand2, dir, _edgeClimb.tazRayDistance, _edgeClimb.wallLayerMask);
            return !foreHeadHit && tazHit;
        }
        
        Vector2 foreHead = _colorPositioning.pointsGroup[ColorPosNameConst.BOOBS].FirstActivePoint();
        Vector2 taz = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();

        foreHeadHit = Physics2D.Raycast(foreHead, dir, _edgeClimb.foreHeadRayDistance, _edgeClimb.wallLayerMask);
        tazHit = Physics2D.Raycast(taz, dir, _edgeClimb.tazRayDistance, _edgeClimb.wallLayerMask);

        return !foreHeadHit && tazHit;
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Vector2 dir = owner.transform.right * owner.transform.localScale.x;
        if (_animationComponent.currentState == "VerticalWallRun")
        {
            Gizmos.color = Color.green;
            Vector2 hand = _colorPositioning.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
            Vector2 hand2 = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();
            
            Gizmos.DrawRay(hand, dir * _edgeClimb.foreHeadRayDistance);
            Gizmos.DrawRay(hand2, dir * _edgeClimb.tazRayDistance);
        }
        else
        {
            Gizmos.DrawRay(_colorPositioning.pointsGroup[ColorPosNameConst.BOOBS].FirstActivePoint(), dir * _edgeClimb.foreHeadRayDistance);
            Gizmos.DrawRay(_colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(), dir * _edgeClimb.tazRayDistance);   
        }
        Gizmos.DrawRay(ForeHeadCheckPos(), Vector2.down * _edgeClimb.floorCheckDistance);
        Gizmos.DrawRay(ForeHeadCheckPos(), Vector2.up  * _edgeClimb.heightHeadRayDistance);
    }
    public void StopCoroutineSafely()
    {
        if(_edgeClimb.EdgeStuckProcess == null)
            return;
        owner.StopCoroutine(_edgeClimb.EdgeStuckProcess);
        ResetPlayerPhysics();
        _edgeClimb.Reset();
        _edgeClimb.EdgeStuckProcess = null;
    }
    public void Dispose()
    {
        ((PlayerController)owner).input.GetState().Jump.started -= jumpHandle;
        owner.OnGizmosUpdate += OnDrawGizmos;
    }
}
    
    [System.Serializable]
    public class WallEdgeClimbComponent : IComponent
    {
        public float tazRayDistance;
        public float floorCheckDistance;
        public float foreHeadRayDistance;
        public float foreHeadRayDistanceTemp;
        public float heightHeadRayDistance;
        public float floorCheckPosFromPlayer;
        public float floorCheackPosFromPlayerTemp;
        public LayerMask wallLayerMask;
        public Coroutine EdgeStuckProcess;
        public Sprite waitSprite;

        public void SaveTemp()
        {
            foreHeadRayDistanceTemp = foreHeadRayDistance;
            floorCheackPosFromPlayerTemp = floorCheckPosFromPlayer;
        }

        public void Reset()
        {
            foreHeadRayDistance = foreHeadRayDistanceTemp;
            floorCheckPosFromPlayer = floorCheackPosFromPlayerTemp;
        }
    }
}
