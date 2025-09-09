using System;
using System.Collections;
using System.Linq;
using Controllers;
using DG.Tweening;
using States;
using UnityEngine;

namespace Systems
{
    public class LedgeClimbSystem : BaseSystem, IStopCoroutineSafely, IDisposable
{
        private ColorPositioningComponent _colorPositioning;
        private WallEdgeClimbComponent _edgeClimbComponent;
        private MoveComponent _moveComponent;
        private AnimationComponentsComposer _animationComponent;
        private GroundingComponent _groundingComponent;
        private FSMSystem _fsm;
        private FsmComponent _fsmComponent;
        private Action<bool> jumpHandle;
        private ControllersBaseFields _baseFields;
        private JumpComponent jumpComponent;

        private RaycastHit2D[] _hitsCache;

        private Nullable<RaycastHit2D> _surfaceHitCache;

        private bool isSecondState;

        private Coroutine _fallOptionHandler;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _moveComponent = owner.GetControllerComponent<MoveComponent>();
            _animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
            _edgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _fsmComponent = owner.GetControllerComponent<FsmComponent>();
            jumpComponent = owner.GetControllerComponent<JumpComponent>();
            _fsm = owner.GetControllerSystem<FSMSystem>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            jumpHandle = c =>
            {
                if (_edgeClimbComponent.EdgeStuckProcess != null)
                {
                    var secTemp = isSecondState;
                    StopCoroutineSafely();
                    _baseFields.rb.linearVelocityY = 0;
                    if (!secTemp)
                    {
                        float extraBoost = 1.3f; // подбери под себя
                        _baseFields.rb.linearVelocity = new Vector2(_baseFields.rb.linearVelocity.x, extraBoost);
                    }
                    _baseFields.rb.AddForce(jumpComponent.jumpDirection * jumpComponent.jumpForce, ForceMode2D.Impulse);
                }
            };
            owner.GetControllerSystem<IInputProvider>().GetState().Jump.started += jumpHandle;
            owner.OnGizmosUpdate += OnDrawGizmos;
        }

        public override void OnUpdate()
        {
            if (_edgeClimbComponent.EdgeStuckProcess == null && _edgeClimbComponent.allowClimb)
                _edgeClimbComponent.EdgeStuckProcess = owner.StartCoroutine(EdgeStuckProcess());
        }

        private IEnumerator EdgeStuckProcess()
        {
            OffPhysics();
            SetDataBeforeStuck();

            yield return WaitUntilClimbPossible();

            yield return ClimbProcess();
        }

        private void SetDataBeforeStuck()
        {
            _edgeClimbComponent.allowClimb = false;
            jumpComponent.isJumpCuted = false;
        }

        private IEnumerator WaitUntilClimbPossible()
        {
            yield return null;
            _animationComponent.PlayState("WallEdgeClimb");
            _animationComponent.SetSpeedAll(0);
            bool headClear;
            bool surfaceExist;
            int flip = (int)owner.transform.localScale.x;

            _fallOptionHandler = owner.StartCoroutine(WaitFallOption(a => StopCoroutineSafely()));

            do
            {
                yield return null;
                headClear = CheckCeil();
                surfaceExist = CheckEdgeSurface();
            }
            while (!headClear && surfaceExist);

            owner.StopCoroutine(_fallOptionHandler);
            _animationComponent.SetSpeedAll(1);
        }

        private IEnumerator ClimbProcess()
        {
            while (_colorPositioning.pointsGroup[ColorPosNameConst.TAZ].searchingRenderer.sprite != _edgeClimbComponent.waitSprite)
            {
                yield return null;
            }
            isSecondState = true;

            // Берём точку удара, а не центр объекта
            Vector2 hitPoint = _surfaceHitCache.Value.point;

            // Смещаем игрока так, чтобы его ноги оказались чуть выше поверхности
            float offset = 0.4f; // подстрой под рост персонажа
            transform.position = new Vector2(transform.position.x, hitPoint.y + offset);


            Action<bool> afterClimb = result => 
            {
                if (result)
                    Climb();
                else
                    StopCoroutineSafely();
            };

            yield return WaitForClimbDecision(afterClimb);
        }

        private void Climb()
        {
            Vector2 hitPoint = _surfaceHitCache.Value.point;

            float offset = 0.8f;
            transform.position = new Vector2(hitPoint.x, hitPoint.y + offset);
            StopCoroutineSafely();
        }

        private IEnumerator WaitForClimbDecision(Action<bool> onResult)
        {
            int flip = (int)owner.transform.localScale.x;

            while (true)
            {

                var headClear = CheckCeil();

                if (headClear && _moveComponent.direction.x == flip && _surfaceHitCache.HasValue)
                {
                    yield return new WaitForSeconds(0.2f);
                    onResult?.Invoke(true);
                    yield break;
                }

                if (_moveComponent.direction.x != flip && _moveComponent.direction.x != 0)
                {
                    yield return new WaitForSeconds(0.3f);
                    onResult?.Invoke(false);
                    yield break;
                }
                yield return null;
            }
        }


        private IEnumerator WaitClimbOption(Action<bool> onResult)
        {
            int flip = (int)owner.transform.localScale.x;

            var headClear = CheckCeil();
            var surfaceExist = CheckEdgeSurface();
            while (true)
            {
                if (headClear && _moveComponent.direction.x == flip && !surfaceExist)
                {
                    yield return new WaitForSeconds(0.2f);
                    onResult?.Invoke(true);
                }
                yield return null;
            }
        }

        private IEnumerator WaitFallOption(Action<bool> onResult)
        {
            int flip = (int)owner.transform.localScale.x;
            while (true)
            {
                if (_moveComponent.direction.x != flip && _moveComponent.direction.x != 0)
                {
                    yield return new WaitForSeconds(0.3f);
                    onResult?.Invoke(false);
                    yield break;
                }
                yield return null;
            }
        }

        private bool CheckCeil()
        {
            Vector2 size = isSecondState ? _edgeClimbComponent.ceilCheckSizeAfter : _edgeClimbComponent.ceilCheckSize;
            var hit = Physics2D.BoxCast(_colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint() + Vector2.up * _edgeClimbComponent.ceilCheckRayDistance, size, 0, Vector2.up,0, _edgeClimbComponent.wallLayer);
            bool headClear = !hit.collider;
            return headClear;
        }

        public bool CheckEdgeSurface()
        {
            var point = _edgeClimbComponent.rayPoint;

            var viewDir = owner.transform.localScale.x * (Vector2)owner.transform.right;

            var pos = (Vector2)_edgeClimbComponent.rayPoint.position + (viewDir * 0.3f);

            float capsuleRadius = 0.0625f; // ~2 пикселя
            Vector2 capsuleSize = new Vector2(capsuleRadius * 2f, capsuleRadius * 2f);

            var hit = Physics2D.CapsuleCast(
                    pos,               // центр
                    capsuleSize,                  // размер капсулы
                    CapsuleDirection2D.Vertical,  // направление "длинной оси" капсулы (тут всё равно т.к. она почти круглая)
                    0f,                           // угол поворота капсулы
                            owner.transform.up * -1,                      // направление
                    _edgeClimbComponent.surfaceCheckDist,                     // длина "луча"
                    _edgeClimbComponent.wallLayer // слой стены
                );

            Debug.DrawRay(pos, owner.transform.up * -1 * _edgeClimbComponent.surfaceCheckDist, hit ? Color.green : Color.yellow);
            _surfaceHitCache = hit;
            return _surfaceHitCache.Value;
        }

        private void OffPhysics()
        {
            var rb = _baseFields.rb;

            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        private void OnPhysics()
        {
            var rb = _baseFields.rb;

            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        public void StickToWall()
        {
            var nearestHit = _hitsCache
                .Where(hit => hit.collider != null)
                .OrderBy(hit => hit.distance)
                .FirstOrDefault();

            owner.transform.position = nearestHit.point;
        }


        public bool CanGrabLedge()
        {
            var point = _edgeClimbComponent.rayPoint;

            var viewDir = owner.transform.localScale.x * (Vector2)owner.transform.right;
            var downDir = (Vector2)owner.transform.up * -1;
            int rayCount = _edgeClimbComponent.rayCount;
            float distance = _edgeClimbComponent.raydistance;

            _hitsCache = new RaycastHit2D[rayCount];
            int hitCount = 0;

            // радиус капсулы (толщина сенсора)
            float capsuleRadius = 0.0625f; // подстрой под размеры персонажа
            Vector2 capsuleSize = new Vector2(capsuleRadius * 2f, capsuleRadius * 2f);

            for (int i = 0; i < rayCount; i++)
            {
                float t = i / (float)rayCount;
                Vector2 currDir = ((1 - t) * viewDir + t * downDir).normalized;

                // CapsuleCast вместо Raycast
                var hit = Physics2D.CapsuleCast(
                    point.position,               // центр
                    capsuleSize,                  // размер капсулы
                    CapsuleDirection2D.Vertical,  // направление "длинной оси" капсулы (тут всё равно т.к. она почти круглая)
                    0f,                           // угол поворота капсулы
                    currDir,                      // направление
                    distance,                     // длина "луча"
                    _edgeClimbComponent.wallLayer // слой стены
                );

                // Визуализация отрезком (иначе Debug.DrawRay не поддерживает капсулы)
                Debug.DrawRay(point.position, currDir * distance, hit ? Color.green : Color.red);

                _hitsCache[i] = hit;

                if (i != 0 && i != rayCount - 1) // не считать view и down
                {
                    if (hit.collider != null)
                        hitCount++;
                }
            }

            bool viewFree = _hitsCache[0].collider == null;
            bool downFree = _hitsCache[rayCount - 1].collider == null;

            // проверка процента
            int midCount = rayCount - 2;
            float ratio = midCount > 0 ? hitCount / (float)midCount : 0f;

            return viewFree && downFree && ratio >= 0.3f; // оставила твои 30%
        }


        public void Dispose()
        {
            ((PlayerController)owner).input.GetState().Jump.started -= jumpHandle;
        }

        public void StopCoroutineSafely()
        {
            if(_edgeClimbComponent.EdgeStuckProcess != null) 
                owner.StopCoroutine(_edgeClimbComponent.EdgeStuckProcess);
            if(_fallOptionHandler != null)
                owner.StopCoroutine(_fallOptionHandler);

            OnPhysics();
            _animationComponent.SetSpeedAll(1);
            _edgeClimbComponent.EdgeStuckProcess = null;
            isSecondState = false;
            _surfaceHitCache = null;
            if(_edgeClimbComponent.allowClimb == false)
                owner.StartCoroutine(Delay());
        }

        public IEnumerator Delay()
        {
            yield return new WaitForSeconds(0.1f);
            _edgeClimbComponent.allowClimb = true;
        }

        public void OnDrawGizmos()
        {
            Vector2 origin = _colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint();
            Vector2 size = isSecondState ? _edgeClimbComponent.ceilCheckSizeAfter : _edgeClimbComponent.ceilCheckSize;
            Vector2 direction = Vector2.up;
            float distance = _edgeClimbComponent.ceilCheckRayDistance;

            Gizmos.color = CheckCeil() ? Color.cyan : Color.green;
            Gizmos.DrawWireCube(origin + direction * distance, size);

            Gizmos.color = Color.white;
        }


    }

    [System.Serializable]
    public class WallEdgeClimbComponent : IComponent
    {
        public Coroutine EdgeStuckProcess;

        [Header("Rays Setting")]
        public Transform rayPoint;
        public int rayCount;
        public float raydistance,ceilCheckRayDistance,surfaceCheckDist;
        public Vector2 ceilCheckSize = new Vector2(0.5f, 1);
        public Vector2 ceilCheckSizeAfter = new Vector2(0.5f, 1);

        [Header("Other")]

        public bool allowClimb;

        public LayerMask wallLayer;

        public Sprite waitSprite;
    }

    [System.Serializable]
    public struct StickyHandsComponent : IComponent
    {
        public LayerMask stickyWallLayer;
        public Transform leftHandPivot, RightHandPivot;
    }


    public class StickyHandsSystem : BaseSystem
    {

        private GroundingComponent _groundingComponent;
        private StickyHandsComponent _stickyHandsComponent;
        private ColorPositioningComponent _colorPositioning;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _stickyHandsComponent = owner.GetControllerComponent<StickyHandsComponent>();
            _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
        }

        public override void OnUpdate()
        {
            StickyHands();
        }

        void RotateHandPivot(Transform handPivot, Vector2 dir, Vector2 lookDir)
        {
            // целевой угол (сдвинутый, т.к. 0° = вниз)
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

            // текущий угол пивота
            float currentAngle = handPivot.eulerAngles.z;

            // нормализуем углы в диапазон [-180,180]
            float delta = Mathf.DeltaAngle(currentAngle, targetAngle);

            // ограничиваем поворот так, чтобы рука не заезжала "за спину"
            // например, ±100° относительно направления взгляда
            float maxFrontAngle = 100f;

            // смотрим, совпадает ли рука с направлением взгляда
            if (lookDir.x > 0) // смотрим вправо
            {
                delta = Mathf.Clamp(delta, -maxFrontAngle, maxFrontAngle);
            }
            else // смотрим влево
            {
                delta = Mathf.Clamp(delta, -maxFrontAngle, maxFrontAngle);
            }

            float finalAngle = currentAngle + delta;

            handPivot.DOKill();
            handPivot.DORotate(new Vector3(0, 0, finalAngle), 0.2f)
                     .SetEase(Ease.OutSine);
        }

        private bool AdjustHand(Transform handPivot, Vector2 handPos, float reachRadius, Vector2 lookDir)
        {
            Collider2D wall = Physics2D.OverlapCircle(handPos, reachRadius, _stickyHandsComponent.stickyWallLayer);

            if (wall != null)
            {
                RaycastHit2D hit = Physics2D.Raycast(handPos, handPivot.right, 0.2f, _stickyHandsComponent.stickyWallLayer);
                if (hit.collider != null)
                {
                    // считаем две касательные
                    Vector2 tangentA = new Vector2(-hit.normal.y, hit.normal.x);
                    Vector2 tangentB = new Vector2(hit.normal.y, -hit.normal.x);

                    // выбираем касательную, совпадающую с направлением взгляда
                    Vector2 chosenTangent = Vector2.Dot(tangentA, lookDir) > Vector2.Dot(tangentB, lookDir)
                        ? tangentA
                        : tangentB;

                    float angle = Mathf.Atan2(chosenTangent.y, chosenTangent.x) * Mathf.Rad2Deg;

                    handPivot.DOKill();
                    handPivot.DORotate(new Vector3(0, 0, angle), 0.2f)
                             .SetEase(Ease.OutSine);
                }
                return true;
            }
            return false;
        }

        public void ReturnToNormal()
        {
            Transform leftPivot = _stickyHandsComponent.leftHandPivot;
            Transform rightPivot = _stickyHandsComponent.RightHandPivot;

            if (leftPivot.rotation == Quaternion.Euler(0, 0, 0) && rightPivot.rotation == Quaternion.Euler(0, 0, 0))
                return;

            leftPivot.DOKill();
            rightPivot.DOKill();

            leftPivot.rotation = Quaternion.Euler(0, 0, 0);
            rightPivot.rotation = Quaternion.Euler(0, 0, 0);
        }

        private void StickyHands()
        {
            Transform leftPivot = _stickyHandsComponent.leftHandPivot;
            Transform rightPivot = _stickyHandsComponent.RightHandPivot;

            // === Если на земле, руки возвращаются в нейтральное положение ===
            Collider2D collider2D = Physics2D.OverlapCircle(owner.transform.position, 0.6f, _stickyHandsComponent.stickyWallLayer);

            Vector2 lookDir = owner.transform.localScale.x < 0 ? Vector2.right : Vector2.left;
            float lookAngle = (owner.transform.localScale.x > 0) ? 0f : 180f;

            // === ДОПОЛНИТЕЛЬНЫЙ ФИЛЬТР (рейкасты вверх/вниз) ===
            float checkDistance = 0.6f; // длина лучей, можешь менять
            LayerMask mask = _stickyHandsComponent.stickyWallLayer;

            RaycastHit2D hitUp = Physics2D.Raycast(owner.transform.position, Vector2.up, checkDistance, mask);
            RaycastHit2D hitDown = Physics2D.Raycast(owner.transform.position, Vector2.down, checkDistance, mask);

            if (hitUp.collider != null || hitDown.collider != null)
            {
                // есть стена строго сверху или снизу → сбрасываем руки и выходим
                leftPivot.DOKill();
                rightPivot.DOKill();

                leftPivot.DORotate(Vector3.zero, 0.01f).SetEase(Ease.Linear);
                rightPivot.DORotate(Vector3.zero, 0.01f).SetEase(Ease.Linear);
                return;
            }

            // === Если на земле, тоже сброс ===
            float sectorHalfAngle = 90f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(owner.transform.position, 0.6f, mask);

            Collider2D chosen = null;
            foreach (var hit in hits)
            {
                Vector2 dirToHit = ((Vector2)hit.ClosestPoint(owner.transform.position) - (Vector2)owner.transform.position).normalized;
                float hitAngle = Mathf.Atan2(dirToHit.y, dirToHit.x) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(lookAngle, hitAngle);

                if (Mathf.Abs(delta) <= sectorHalfAngle)
                {
                    chosen = hit;
                    break;
                }
            }

            if (_groundingComponent.isGround || chosen == null)
            {
                leftPivot.DOKill();
                rightPivot.DOKill();

                leftPivot.DORotate(Vector3.zero, 0.01f).SetEase(Ease.Linear);
                rightPivot.DORotate(Vector3.zero, 0.01f).SetEase(Ease.Linear);
                return;
            }

            // === Получаем позиции рук ===
            Vector2 leftHandPos = _colorPositioning.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
            Vector2 rightHandPos = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();

            // === Базовое направление: вверх + в сторону взгляда ===
            Vector2 targetDir = (-Vector2.up + lookDir).normalized;

            RotateHandPivot(leftPivot, targetDir, lookDir);
            RotateHandPivot(rightPivot, targetDir, lookDir);

            AdjustHand(leftPivot, leftHandPos, 0.1f, lookDir);
            AdjustHand(rightPivot, rightHandPos, 0.1f, lookDir);
        }

    }
}
