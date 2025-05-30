using System.Collections;
using Assets.Scripts;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{
    public class WallRunSystem : BaseSystem
    {
        private WallRunComponent _wallRunComponent;
        private ColorPositioningComponent _colorPositioningComponent;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        private LedgeClimbSystem _wallEdge;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private AnimationComponent _animationComponent;
        private DashComponent _dashComponent;
        private FSMSystem _fsmSystem;
        
        

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _moveComponent = owner.GetControllerComponent<MoveComponent>();
            _jumpComponent = owner.GetControllerComponent<JumpComponent>();
            _animationComponent = owner.GetControllerComponent<AnimationComponent>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _wallEdge = owner.GetControllerSystem<LedgeClimbSystem>();
            _fsmSystem = owner.GetControllerSystem<FSMSystem>();
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            owner.OnGizmosUpdate += OnGizmosDraw;
            owner.OnUpdate += Timers;
        }

        public override void OnUpdate()
        {
            if (_wallRunComponent.wallRunProcess == null)
            {
                _wallRunComponent.canWallRun = false;
                _wallRunComponent.wallRunProcess = owner.StartCoroutine(WallRunProcess());
            }
        }

        public void Timers()
        {
            if ((_jumpComponent.isGround || _wallEdgeClimbComponent.EdgeStuckProcess != null))
            {
                _wallRunComponent.canWallRun = true;
                _wallRunComponent.isJumped = false;
            }

            if (_wallRunComponent.isJumped && _wallRunComponent.timeToPunish)
            {
                if (_wallRunComponent.jumpDirection != owner.transform.localScale.x)
                {
                    ((EntityController)owner).baseFields.rb.linearVelocity = Vector2.zero;
                    _dashComponent.allowDash = false;
                    _wallRunComponent.canWallRun = false;
                    _wallRunComponent.timeToPunish = false;
                    _wallRunComponent.isJumped = false;
                    
                }
            }
        }

        public bool CanStartWallRun()
        {
            Vector2 dir = Vector2.right * owner.transform.localScale.x;
            var handHit = Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), dir, _wallRunComponent.wallRunCheckDist, _wallRunComponent.wallLayer);
            var legHit = Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up/2.6f, dir, _wallRunComponent.wallRunCheckDist, _wallRunComponent.wallLayer);
            return handHit.collider && legHit.collider && !_jumpComponent.isGround;
        }

        private IEnumerator WallRunProcess()
        {
            var rb = ((EntityController)owner).baseFields.rb;
            float climbDistance = _wallRunComponent.wallRunDistance;
            float duration = _wallRunComponent.wallRunDuration;
            float direction = owner.transform.localScale.x;
            _wallRunComponent.isJumped = false;
            float elapsed = 0f;
            bool jumpQueued = false;
            float fallGraceTime = 0.1f;
            _wallRunComponent.timeToPunish = false;
            rb.gravityScale = 0f;
            rb.linearVelocityY += 4f;

            yield return new WaitForSeconds(0.05f);
            _dashComponent.allowDash = false;
            Vector2 startPos = rb.position;
            Vector2 targetPos = startPos + Vector2.up * climbDistance;

            float lostDirTime = 0f;

            while (elapsed < duration && !Mathf.Approximately(rb.position.y, targetPos.y))
            {
                var inputState = ((PlayerController)owner).input.GetState();

                Vector2 handPos = _colorPositioningComponent.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
                Vector2 legPos = _colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up / 2.6f;

                RaycastHit2D handHit = Physics2D.Raycast(handPos, Vector2.right * direction, _wallRunComponent.wallRunCheckDist / 2f, _wallRunComponent.wallLayer);
                RaycastHit2D legHit = Physics2D.Raycast(legPos, Vector2.right * direction, _wallRunComponent.wallRunCheckDist, _wallRunComponent.wallLayer);
                _wallRunComponent.isWallValid = handHit && legHit;

                bool isCeiling = Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint() + new Vector2(direction / 5f, 0), Vector2.up, 0.4f, _wallRunComponent.wallLayer);
                
                if (!jumpQueued && inputState.inputActions.Player.Jump.WasPressedThisFrame())
                {
                    jumpQueued = true;
                }
                
                if (_moveComponent.direction.x != direction)
                {
                    lostDirTime += Time.deltaTime;
                }
                else
                {
                    lostDirTime = 0f;
                }
                
                if (!_wallRunComponent.isWallValid || isCeiling || lostDirTime > fallGraceTime || _wallEdge.CanGrabLedge(out _, out _))
                {
                    if (jumpQueued && _moveComponent.direction.x != 0)
                    {
                        rb.linearVelocity = new Vector2(-direction * _wallRunComponent.jumpAwayForce, _wallRunComponent.jumpUpForce);
                        _wallRunComponent.isJumped = true;
                    }
                    if (!_wallRunComponent.isWallValid && !jumpQueued && !isCeiling)
                    {
                        Vector2 dovodka = (rb.position += new Vector2(0, 0.2f));
                        while (rb.position == dovodka)
                        {
                            Vector2.MoveTowards(rb.position,dovodka,0.05f);
                            yield return null;
                        }
                        _fsmSystem.SetState(new WallLeangeClimb((EntityController)owner));
                    }
                    break;
                }
                
                if (jumpQueued)
                {
                    rb.linearVelocity = new Vector2(-direction * _wallRunComponent.jumpAwayForce, _wallRunComponent.jumpUpForce);
                    _wallRunComponent.isJumped = true;
                    break;
                }
                
                float t = elapsed / duration;
                float curveT = Mathf.Sin(t * Mathf.PI * 0.5f);
                Vector2 newPos = new Vector2(rb.position.x, Mathf.Lerp(startPos.y, targetPos.y, curveT));
                rb.MovePosition(newPos);

                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!_wallRunComponent.isJumped)
            {
                _animationComponent.CrossFade("FallDown", 0.2f);
                rb.linearVelocity = new Vector2(0, Mathf.Min(rb.linearVelocity.y, 0));
            }
            else
            {
                _dashComponent.allowDash = true;
                _animationComponent.CrossFade("FallUp", 0.2f);
                _wallRunComponent.canWallRun = true;
            }
            _wallRunComponent.isWallValid = false;
            rb.gravityScale = 1f;
            yield return new WaitForSeconds(0.07f);
            if (_wallRunComponent.isJumped)
                _wallRunComponent.jumpDirection = -direction;
            _wallRunComponent.wallRunProcess = null;
            _wallRunComponent.timeToPunish = true;
        }


        public void OnGizmosDraw()
        {
            float direction = owner.transform.localScale.x;
            Vector2 dir = Vector2.right * owner.transform.localScale.x * _wallRunComponent.wallRunCheckDist;
            bool wallValid =
                Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint(), Vector2.right * direction, _wallRunComponent.wallRunCheckDist*2, _wallRunComponent.wallLayer) &&
                Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up/2.6f, Vector2.right * direction, _wallRunComponent.wallRunCheckDist*2, _wallRunComponent.wallLayer);
            if (!wallValid)
            {
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), dir);
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up / 2.6f, dir);
            }
            else
            {
                Gizmos.color = Color.green;
                dir = Vector2.right * owner.transform.localScale.x * _wallRunComponent.wallRunCheckDist;
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint()+ new Vector2(owner.transform.localScale.x/5,0), Vector2.up * 0.4f);
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint(), dir/2);
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up / 2.6f, dir);
            }
        }
    }

    [System.Serializable]
    public class WallRunComponent : IComponent
    {
        public Coroutine wallRunProcess;
        public bool canWallRun;
        public float wallRunDistance = 2f;
        public float wallRunDuration = 0.5f;
        public float jumpAwayForce = 5f;
        public float jumpUpForce = 5f;
        public float wallRunCheckDist = 0.3f;
        public LayerMask wallLayer;
        public bool isWallValid;
        public bool isJumped = false;
        public bool timeToPunish = false;
        public float jumpDirection;
    }
}
