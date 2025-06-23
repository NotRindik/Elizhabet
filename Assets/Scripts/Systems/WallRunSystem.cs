using System;
using System.Collections;
using Assets.Scripts;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{
    public class WallRunSystem : BaseSystem,IDisposable
    {
        private WallRunComponent _wallRunComponent;
        private ColorPositioningComponent _colorPositioningComponent;
        private MoveComponent _moveComponent;
        private GroundingComponent _groundingComponent;
        private LedgeClimbSystem _wallEdge;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private AnimationComponent _animationComponent;
        private DashComponent _dashComponent;
        private SpriteSynchronizer _spriteSynchronizer;
        private FSMSystem _fsmSystem;

        private Coroutine defaultColorProcess;
        
        private float direction;
        
        private float WallRunDistance => Mathf.Max(0f, _wallRunComponent.wallRunDistance - (_wallRunComponent.punishCoeff+0.2f) * _wallRunComponent.sameWallRunCount);
        private float WallRunDuration => Mathf.Max(0f, _wallRunComponent.wallRunDuration - _wallRunComponent.punishCoeff * _wallRunComponent.sameWallRunCount);

        private Color orange = new Color(1.0f, 0.55f, 0.2f);
        private Color red    = new Color(1.0f, 0.0f, 0.0f);
        private Action<bool> jumpHandler;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _moveComponent = owner.GetControllerComponent<MoveComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _animationComponent = owner.GetControllerComponent<AnimationComponent>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _wallEdge = owner.GetControllerSystem<LedgeClimbSystem>();
            _fsmSystem = owner.GetControllerSystem<FSMSystem>();
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            _spriteSynchronizer = owner.GetControllerComponent<SpriteSynchronizer>();
            owner.OnGizmosUpdate += OnGizmosDraw;
            jumpHandler =c =>
            {
                if (_wallRunComponent.wallRunProcess != null)
                {
                    var rb = ((EntityController)owner).baseFields.rb;
                    base.owner.StopCoroutine(_wallRunComponent.wallRunProcess);
                    base.owner.StartCoroutine(FastStop());
                    
                    _wallRunComponent.isJumped = true;
                    _dashComponent.allowDash = true;
                    _wallRunComponent.canWallRun = true;
                    rb.gravityScale = 1;
                    rb.AddForce(new Vector2(-direction * _wallRunComponent.jumpAwayForce, _wallRunComponent.jumpUpForce), ForceMode2D.Impulse);

                }
            };
            ((PlayerController)owner).input.GetState().Jump.started += jumpHandler;
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
            if ((_groundingComponent.isGround || _wallEdgeClimbComponent.EdgeStuckProcess != null))
            {
                _wallRunComponent.canWallRun = true;
                _wallRunComponent.isJumped = false;
                direction = 0;
                _wallRunComponent.sameWallRunCount = 0;
            }
            if (_wallRunComponent.isJumped &&  ((EntityController)owner).baseFields.rb.linearVelocityY < 0)
            {
                _dashComponent.ghostTrail.StopTrail();
            }
        }

        public bool CanStartWallRun()
        {
            Vector2 dir = Vector2.right * owner.transform.localScale.x;
            var handHit = Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), dir, _wallRunComponent.wallRunCheckDist, _wallRunComponent.wallLayer);
            var legHit = Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up/2.6f, dir, _wallRunComponent.wallRunCheckDist, _wallRunComponent.wallLayer);
            return handHit.collider && legHit.collider && !_groundingComponent.isGround;
        }

        private IEnumerator WallRunProcess()
        {
            
            var rb = ((EntityController)owner).baseFields.rb;
            float climbDistance = WallRunDistance;
            float duration = WallRunDuration;
            if(direction != owner.transform.localScale.x)
                direction = owner.transform.localScale.x;
            else
            {
                _wallRunComponent.sameWallRunCount++;
            }
            if (defaultColorProcess != null)
            {
                owner.StopCoroutine(defaultColorProcess);
                defaultColorProcess = null;
            }
            _wallRunComponent.isJumped = false;
            float elapsed = 0f;
            float fallGraceTime = 0.1f;
            rb.gravityScale = 0f;
            rb.linearVelocityY += 4f;

            yield return new WaitForSeconds(0.05f);
            _dashComponent.allowDash = false;
            _dashComponent.ghostTrail.StartTrail();
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
                    if (!_wallRunComponent.isWallValid  && !isCeiling)
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
                
                float t = elapsed / duration;

                if (t >= 0f && t < 0.1f)
                {
                    // t от 0 до 0.2 → нормализуем t к [0..1] делением на 0.2
                    float tNorm = t / 0.1f;
                    _spriteSynchronizer.hairSprire.color = Color.Lerp(Color.white, orange, tNorm);   
                }
                else
                {
                    // t от 0.2 до 1 → нормализуем t к [0..1] относительно [0.2..1]
                    float tNorm = (t - 0.1f) / 0.9f;
                    _spriteSynchronizer.hairSprire.color = Color.Lerp(orange, red, tNorm);   
                }
                
                float curveT = Mathf.Sin(t * Mathf.PI * 0.5f);
                Vector2 newPos = new Vector2(rb.position.x, Mathf.Lerp(startPos.y, targetPos.y, curveT));
                rb.MovePosition(newPos);

                elapsed += Time.deltaTime;
                yield return new WaitForFixedUpdate();
            }
            _animationComponent.CrossFade("FallDown", 0.2f);
            if (!_wallRunComponent.isJumped)
            {
                rb.linearVelocity = new Vector2(0, Mathf.Min(rb.linearVelocity.y, 0));
                _dashComponent.ghostTrail.StopTrail();
            }
            _wallRunComponent.isWallValid = false;
            rb.gravityScale = 1f;
            yield return FastStop();
        }

        public IEnumerator MoveTowardColorProccess(Color color,float delta)
        {
            while (_spriteSynchronizer.hairSprire.color != color)
            {
                _spriteSynchronizer.hairSprire.color = Vector4.MoveTowards(_spriteSynchronizer.hairSprire.color,color,delta);
                yield return null;
            }
            defaultColorProcess = null;
        }

        public IEnumerator FastStop()
        {
            if (defaultColorProcess == null)
            {
                defaultColorProcess = owner.StartCoroutine(MoveTowardColorProccess(new Color(1, 1, 1, 1),0.1f));
            }
            else
            {
                owner.StopCoroutine(defaultColorProcess);
                defaultColorProcess = owner.StartCoroutine(MoveTowardColorProccess(new Color(1, 1, 1, 1),0.1f));
            }
            yield return new WaitForSeconds(0.04f);
            _wallRunComponent.wallRunProcess = null;
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
        public void Dispose()
        {
            ((PlayerController)owner).input.GetState().Jump.started -= jumpHandler;
            owner.OnUpdate -= Timers;
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
        public float jumpDirection;
        public float punishCoeff = 0.4f;
        public int sameWallRunCount;
    }
}