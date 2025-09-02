using System;
using System.Collections;
using Assets.Scripts;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{
    public class WallRunSystem : BaseSystem, IDisposable, IStopCoroutineSafely
    {
        private WallRunComponent _wallRunComponent;
        private ColorPositioningComponent _colorPositioningComponent;
        private MoveComponent _moveComponent;
        private GroundingComponent _groundingComponent;
        private LedgeClimbSystem _wallEdge;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private AnimationComponentsComposer _animationComponent;
        private DashComponent _dashComponent;
        private ControllersBaseFields _baseFields;
        private RendererCollection _spriteSynchronizer;
        private IInputProvider _inputProvider;
        private FSMSystem _fsmSystem;

        private SpriteFlipSystem _spriteFlipSystem;
        private Coroutine _defaultColorProcess;
        private Coroutine _coyotoTimeProcess;
        
        private float direction;

        private float elapsed;
        
        private float WallRunDistance => Mathf.Max(0f, _wallRunComponent.wallRunDistance - (_wallRunComponent.punishCoeff+0.2f) * _wallRunComponent.sameWallRunCount);
        private float WallRunDuration => Mathf.Max(0, _wallRunComponent.wallRunDuration - _wallRunComponent.punishCoeff * _wallRunComponent.sameWallRunCount);



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
            _animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _wallEdge = owner.GetControllerSystem<LedgeClimbSystem>();
            _fsmSystem = owner.GetControllerSystem<FSMSystem>();
            _dashComponent = owner.GetControllerComponent<DashComponent>();
            _spriteSynchronizer = owner.GetControllerComponent<RendererCollection>();
            _inputProvider = owner.GetControllerSystem<IInputProvider>();
            _spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
            owner.OnGizmosUpdate += OnGizmosDraw;
            jumpHandler =c =>
            {
                if (_wallRunComponent.wallRunProcess != null || _wallRunComponent.currCoyotoTime > 0)
                {
                    _wallRunComponent.currCoyotoTime = 0;
                    if(_wallRunComponent.wallRunProcess != null)
                        owner.StopCoroutine(_wallRunComponent.wallRunProcess);
                    _wallRunComponent.isJumped = true;
                    _spriteFlipSystem.IsActive = true;
                    owner.StartCoroutine(FastStop());
                    owner.StartCoroutine(ApplyJumpForceDelayed());

                }
            };
            _inputProvider.GetState().Jump.started += jumpHandler;
            owner.OnUpdate += Timers;
        }
        private IEnumerator ApplyJumpForceDelayed()
        {
            yield return new WaitForFixedUpdate();
            var rb = _baseFields.rb;
            if(_wallRunComponent.oldVelocity != Vector2.zero)
                rb.linearVelocity = _wallRunComponent.oldVelocity;
            _dashComponent.allowDash = true;
            _wallRunComponent.canWallRun = true;
            _dashComponent.ghostTrail.StartTrail();
            rb.gravityScale = 1;
            float t = Mathf.Clamp01(elapsed / WallRunDuration);
            Debug.Log($"elapse: {t}, Distance: {WallRunDistance}, Duration: {WallRunDuration}");

            rb.AddForce(new Vector2(-direction * _wallRunComponent.jumpAwayForce, _wallRunComponent.jumpUpForce) * (t), ForceMode2D.Impulse);
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
            if (_wallRunComponent.isJumped && _baseFields.rb.linearVelocityY <= 0)
            {
                _dashComponent.ghostTrail.StopTrail();
            }

            if ((_groundingComponent.isGround || _wallEdgeClimbComponent.EdgeStuckProcess != null))
            {
                _wallRunComponent.canWallRun = true;
                _wallRunComponent.isJumped = false;
                direction = 0;
                _wallRunComponent.sameWallRunCount = 0;
            }


            if (_wallEdge.CanGrabLedge() && _wallRunComponent.wallRunProcess != null)
            {
                StopCoroutineSafely();
                _fsmSystem.SetState(new WallLeangeClimb((EntityController)owner));
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
            var rb = _baseFields.rb;
            if(direction != owner.transform.localScale.x)
                direction = owner.transform.localScale.x;
            else
            {
                _wallRunComponent.sameWallRunCount++;
            }
            if (_defaultColorProcess != null)
            {
                owner.StopCoroutine(_defaultColorProcess);
                _defaultColorProcess = null;
            }
            elapsed = 0f;
            float fallGraceTime = 0.4f;
            rb.gravityScale = 0f;
            rb.linearVelocityY += 4f;
            _wallRunComponent.isJumped = false;
            _spriteFlipSystem.IsActive = false;
            yield return new WaitForSeconds(0.05f);
            _dashComponent.allowDash = false;
            _dashComponent.ghostTrail.StartTrail();
            _wallRunComponent.oldVelocity = Vector2.zero;
            Vector2 startPos = rb.position;
            Vector2 targetPos = startPos + Vector2.up * WallRunDistance;
            _wallRunComponent.currCoyotoTime = _wallRunComponent.coyotoTime;
            float lostDirTime = 0f;
            while (elapsed < WallRunDuration && !Mathf.Approximately(rb.position.y, targetPos.y))
            {

                Vector2 handPos = _colorPositioningComponent.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
                Vector2 legPos = _colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up / 2.6f;

                RaycastHit2D handHit = Physics2D.Raycast(handPos, Vector2.right * direction, _wallRunComponent.wallRunCheckDist / 2f, _wallRunComponent.wallLayer);
                RaycastHit2D legHit = Physics2D.Raycast(legPos, Vector2.right * direction, _wallRunComponent.wallRunCheckDist, _wallRunComponent.wallLayer);
                _wallRunComponent.isWallValid = handHit && legHit;

                bool isCeiling = Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), Vector2.up, 0.4f, _wallRunComponent.wallLayer);
                
                if (_moveComponent.direction.x != direction)
                {
                    lostDirTime += Time.deltaTime;
                }
                else
                {
                    lostDirTime = 0f;
                }
                
                float t = elapsed / WallRunDuration;

                if (t >= 0f && t < 0.1f)
                {
                    // t от 0 до 0.2 → нормализуем t к [0..1] делением на 0.2
                    float tNorm = t / 0.1f;
                    _spriteSynchronizer.renderers["Hair"].color = Color.Lerp(Color.white, orange, tNorm);   
                }
                else
                {
                    // t от 0.2 до 1 → нормализуем t к [0..1] относительно [0.2..1]
                    float tNorm = (t - 0.1f) / 0.9f;
                    _spriteSynchronizer.renderers["Hair"].color = Color.Lerp(orange, red, tNorm);   
                }
                
                float curveT = Mathf.Sin(t * Mathf.PI * 0.5f);
                Vector2 newPos = new Vector2(rb.position.x, Mathf.Lerp(startPos.y, targetPos.y, curveT));
                rb.MovePosition(newPos);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            _spriteFlipSystem.IsActive = true;
            if (!_wallRunComponent.isJumped)
            {
                _wallRunComponent.oldVelocity = rb.linearVelocity;
                rb.linearVelocity = new Vector2(0, Mathf.Min(rb.linearVelocity.y, 0));
                _dashComponent.ghostTrail.StopTrail();
            }
            _wallRunComponent.isWallValid = false;
            rb.gravityScale = 1f;
            if(_coyotoTimeProcess!= null && _wallRunComponent.sameWallRunCount >= 1)
                owner.StopCoroutine(_coyotoTimeProcess);
            owner.StartCoroutine(StartCoyotoTime());
            yield return FastStop();
        }

        public IEnumerator MoveTowardColorProccess(Color color,float delta)
        {
            while (_spriteSynchronizer.renderers["Hair"].color != color)
            {
                _spriteSynchronizer.renderers["Hair"].color = Vector4.MoveTowards(_spriteSynchronizer.renderers["Hair"].color,color,delta);
                yield return null;
            }
            _defaultColorProcess = null;
        }

        public IEnumerator StartCoyotoTime()
        {
            while (_wallRunComponent.currCoyotoTime > 0)
            {
                _wallRunComponent.currCoyotoTime -= Time.deltaTime;
                yield return null;
            }
            _animationComponent.CrossFadeState("FallDown", 0.2f);
        }

        public IEnumerator FastStop()
        {
            if (_defaultColorProcess == null)
            {
                _defaultColorProcess = owner.StartCoroutine(MoveTowardColorProccess(new Color(1, 1, 1, 1),0.1f));
            }
            else
            {
                owner.StopCoroutine(_defaultColorProcess);
                _defaultColorProcess = owner.StartCoroutine(MoveTowardColorProccess(new Color(1, 1, 1, 1),0.1f));
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
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), Vector2.up * 0.4f);
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint(), dir/2);
                Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.RIGHT_LEG].FirstActivePoint() + Vector2.up / 2.6f, dir);
            }
        }
        public void Dispose()
        {
            ((PlayerController)owner).input.GetState().Jump.started -= jumpHandler;
            owner.OnUpdate -= Timers;
        }

        public void StopCoroutineSafely()
        {
            if (_wallRunComponent.wallRunProcess != null)
            {
                owner.StopCoroutine(_wallRunComponent.wallRunProcess);
                owner.StartCoroutine(FastStop());
                                    _wallRunComponent.currCoyotoTime = 0;
                _spriteFlipSystem.IsActive = true;
                _wallRunComponent.isWallValid = false;
                _baseFields.rb.gravityScale = 1f;
                _dashComponent.ghostTrail.StopTrail();
            }
        }
    }

    [System.Serializable]
    public class WallRunComponent : IComponent
    {
        public float coyotoTime = 0.2f;
        public float currCoyotoTime = 0;
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
        public Vector2 oldVelocity;
        public float punishCoeff = 0.4f;
        public int sameWallRunCount;
    }
}