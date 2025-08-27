using System;
using System.Collections;
using Controllers;
using DG.Tweening;
using States;
using UnityEngine;

namespace Systems
{ 
    public class LedgeClimbSystem : BaseSystem,IStopCoroutineSafely,IDisposable
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
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _moveComponent = owner.GetControllerComponent<MoveComponent>();
            _animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
            _edgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _fsmComponent = owner.GetControllerComponent<FsmComponent>();
            _fsm = owner.GetControllerSystem<FSMSystem>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            jumpHandle = c =>
            {
                if (_edgeClimbComponent.EdgeStuckProcess != null)
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
            if (_edgeClimbComponent.EdgeStuckProcess == null)
                _edgeClimbComponent.EdgeStuckProcess = owner.StartCoroutine(EdgeStuckProcess());
        }

        private IEnumerator EdgeStuckProcess()
        {
            if (!CanGrabLedge(out var foreHeadHit, out var tazHit))
            {
                _edgeClimbComponent.EdgeStuckProcess = null;
                yield break;
            }
            var rb = ((EntityController)owner).baseFields.rb;
            bool isStick = TryStickToLedge(tazHit, out var floorHit);
            if (!floorHit || !isStick)
            {
                _edgeClimbComponent.EdgeStuckProcess = null;
                yield break;
            }

            foreach (var item in owner.Systems)
            {
                if (item.Value is IStopCoroutineSafely coroutineStopper)
                {
                    coroutineStopper.StopCoroutineSafely();
                }
            }

            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0;
            rb.bodyType = RigidbodyType2D.Kinematic;
            _edgeClimbComponent.SaveTemp();
            _edgeClimbComponent.floorCheckPosFromPlayer = 0.04f;
            _edgeClimbComponent.foreHeadRayDistance = 0.39f/2;
            _edgeClimbComponent.allowClimb = false;
            int flip = (int)owner.transform.localScale.x;
            bool isClimb = false;

            while (_colorPositioning.pointsGroup[ColorPosNameConst.TAZ].searchingRenderer.sprite != _edgeClimbComponent.waitSprite)
            {
                if(_animationComponent.CurrentState != "WallEdgeClimb") 
                    _animationComponent.PlayState("WallEdgeClimb");

                yield return null;
            }

            while (true)
            {
                yield return null;
                var headClear = !Physics2D.Raycast(_colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), Vector2.up, _edgeClimbComponent.heightHeadRayDistance, _edgeClimbComponent.wallLayerMask);

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
                    yield return new WaitForSeconds(0.3f);

                    break;
                }
                var rot = owner.transform.GetChild(0).transform.eulerAngles;
                float period = 2f;
                float amplitude = -2.3f;

                float angle = Mathf.Sin(Time.time * Mathf.PI * 2f / period) * amplitude;
                rot.z = angle;
                owner.transform.GetChild(0).transform.rotation = Quaternion.Euler(rot);
                floorHit = Physics2D.Raycast(
                    ForeHeadCheckPos(),
                    Vector2.down,
                    _edgeClimbComponent.floorCheckDistance,
                    _edgeClimbComponent.wallLayerMask
                );
            }
            TeleportToClimbPosition(floorHit,isClimb);
            ResetPlayerPhysics();
            _edgeClimbComponent.Reset();
            _edgeClimbComponent.EdgeStuckProcess = null;
            owner.StartCoroutine(WallEdgeClimbDelay());
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
            floorHit = Physics2D.Raycast(
                ForeHeadCheckPos(),
                Vector2.down,
                _edgeClimbComponent.floorCheckDistance,
                _edgeClimbComponent.wallLayerMask
            );

            // Если нет попадания — сразу false
            if (!floorHit.collider)
                return false;

            // Проверяем "слишком близко" (например, меньше 0.05f)
            if (Vector2.Distance(ForeHeadCheckPos(), floorHit.point) < 0.05f)
                return false;

            float delta = 0.5f; // допустимое отклонение
            float floorY = floorHit.point.y;
            float pelvisY = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint().y;

            bool isWithinRange = Mathf.Abs(floorY - pelvisY) <= delta;

            if (Vector3.Dot(floorHit.normal, Vector3.up) < 0.5f || !isWithinRange)
                return false;

            var newX = tazHit.point.x - owner.transform.right.x * 0.1f * owner.transform.localScale.x;
            var newY = floorHit.point.y + 0.41f;
            owner.transform.position = new Vector2(newX, newY);

            return true;
        }

        private Vector2 ForeHeadCheckPos() =>
            _colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint()
            + new Vector2(_edgeClimbComponent.floorCheckPosFromPlayer, 0) * owner.transform.localScale.x;

        public bool CanGrabLedge(out RaycastHit2D foreHeadHit, out RaycastHit2D tazHit)
        {
            Vector2 dir = owner.transform.right * owner.transform.localScale.x;


            if (_animationComponent.CurrentState == "WallRun")
            {
                Vector2 hand = _colorPositioning.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
                Vector2 hand2 = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();

                foreHeadHit = Physics2D.Raycast(hand, dir, _edgeClimbComponent.foreHeadRayDistance, _edgeClimbComponent.wallLayerMask);
                tazHit = Physics2D.Raycast(hand2, dir, _edgeClimbComponent.tazRayDistance, _edgeClimbComponent.wallLayerMask);
                return !foreHeadHit && tazHit;
            }

            // Получаем исходные позиции
            Vector2 boobsPos = _colorPositioning.pointsGroup[ColorPosNameConst.BOOBS].FirstActivePoint();
            Vector2 tazPos = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();

            // Список смещений: сначала 0, потом ±2 пикселя в мировых единицах
            float pixelToUnit = 1f / 32; // если есть pixelsPerUnit
            Vector2[] offsets = new Vector2[]
            {
        Vector2.zero,
        new Vector2(0, -pixelToUnit * 2),
        new Vector2(0, -pixelToUnit * 4)
            };


            foreHeadHit = default;
            tazHit = default;

            foreach (var offset in offsets)
            {
                var fh = Physics2D.Raycast(boobsPos + offset, dir, _edgeClimbComponent.foreHeadRayDistance, _edgeClimbComponent.wallLayerMask);
                var th = Physics2D.Raycast(tazPos + offset, dir, _edgeClimbComponent.tazRayDistance, _edgeClimbComponent.wallLayerMask);

                if (!fh && th) // если нашли успешное сочетание
                {
                    foreHeadHit = fh;
                    tazHit = th;
                    return true;
                }
            }

            return false;
        }


        public void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            Vector2 dir = owner.transform.right * owner.transform.localScale.x;
            if (_animationComponent.CurrentState == "WallRun")
            {
                Gizmos.color = Color.green;
                Vector2 hand = _colorPositioning.pointsGroup[ColorPosNameConst.LEFT_HAND].FirstActivePoint();
                Vector2 hand2 = _colorPositioning.pointsGroup[ColorPosNameConst.RIGHT_HAND_POS].FirstActivePoint();
            
                Gizmos.DrawRay(hand, dir * _edgeClimbComponent.foreHeadRayDistance);
                Gizmos.DrawRay(hand2, dir * _edgeClimbComponent.tazRayDistance);
            }
            else
            {
                Gizmos.DrawRay(_colorPositioning.pointsGroup[ColorPosNameConst.BOOBS].FirstActivePoint(), dir * _edgeClimbComponent.foreHeadRayDistance);
                Gizmos.DrawRay(_colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(), dir * _edgeClimbComponent.tazRayDistance);   
            }
            Gizmos.DrawRay(ForeHeadCheckPos(), Vector2.down * _edgeClimbComponent.floorCheckDistance);
            Gizmos.DrawRay(_colorPositioning.pointsGroup[ColorPosNameConst.HEAD].FirstActivePoint(), Vector2.up  * _edgeClimbComponent.heightHeadRayDistance);
        }
        public void StopCoroutineSafely()
        {
            if(_edgeClimbComponent.EdgeStuckProcess == null)
                return;
            owner.StopCoroutine(_edgeClimbComponent.EdgeStuckProcess);
            ResetPlayerPhysics();
            _edgeClimbComponent.Reset();
            _edgeClimbComponent.EdgeStuckProcess = null;
            owner.StartCoroutine(WallEdgeClimbDelay());
        }

        public IEnumerator WallEdgeClimbDelay()
        {
            yield return new WaitForSeconds(0.1f);
            _edgeClimbComponent.allowClimb = true;
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
        public bool allowClimb = true;

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


            // делаем угол сектора (например, ±90° от взгляда)
            float sectorHalfAngle = 90f;

            // находим коллайдеры в радиусе
            Collider2D[] hits = Physics2D.OverlapCircleAll(owner.transform.position, 0.6f, _stickyHandsComponent.stickyWallLayer);

            // фильтруем по углу
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
