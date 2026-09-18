using System;
using Controllers;
using UnityEngine;
using DG.Tweening;
namespace Systems
{
    public class LungeAttackSystem : BaseSystem
    {
        private LungeAttackComponent _lungeAttackComponent;
        private IInputProvider _inputProvider => _itemComponent._currentOwner.GetControllerSystem<IInputProvider>();
        private ItemComponent _itemComponent => owner.GetControllerComponent<ItemComponent>();
        private Ease ease = Ease.OutCubic;
        private Tween _lungeTween;
        private bool _completedNaturally;

        private Collider2D[] CollidersBuffer = new Collider2D[10];
        private ContactFilter2D filter;

        public bool IsLunging => _lungeTween != null && _lungeTween.IsActive();

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);

            _lungeAttackComponent = owner.GetControllerComponent<LungeAttackComponent>();
            filter = new()
            {
                layerMask = _lungeAttackComponent.enemyLayer,
                useLayerMask = true
            };
        }

        public bool TryLungeAttack(Action<Vector2> onArrived, Action onCancelled = null)
        {
            if (!TryFindTarget(out Vector2 lungeTarget, out Vector2 aimTarget))
                return false;

            LungeTo(lungeTarget, aimTarget, onArrived, onCancelled);
            return true;
        }

        private Rigidbody2D _playerRb;
        private RigidbodyType2D _rbTypeBeforeLunge;

        public void LungeTo(in Vector2 lungeTarget, Vector2 aimTarget, Action<Vector2> onArrived, Action onCancelled = null)
        {
            CancelLunge();

            var player = _itemComponent._currentOwner;
            _playerRb = player.GetControllerComponent<ControllersBaseFields>().rb;

            Vector2 start = player.transform.position;
            Vector2 targetPosition = lungeTarget;

            Vector2 direction = (targetPosition - start).normalized;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;

            float distance = Vector2.Distance(start, targetPosition);

            if(distance <= _lungeAttackComponent.stopDistance)
            {
                onArrived?.Invoke(aimTarget);
                return;
            }

            targetPosition -= direction * _lungeAttackComponent.stopDistance;

            if (_playerRb != null)
            {
                _rbTypeBeforeLunge = _playerRb.bodyType;
                _playerRb.bodyType = RigidbodyType2D.Kinematic;
                _playerRb.linearVelocity = Vector2.zero;
            }

            _completedNaturally = false;
            _virtualT = 0f;

            _lungeTween = DOTween.To(
                    () => _virtualT,
                    x => _virtualT = x,
                    1f,
                    _lungeAttackComponent.duration
                )
                .SetEase(ease)
                .OnUpdate(() =>
                    {
                        Vector2 position = Vector2.Lerp(start, targetPosition, _virtualT);
                        _playerRb.MovePosition(position);
                    }
                )
                .OnComplete(() =>
                    {
                        _completedNaturally = true;
                        RestoreRigidbody();
                        onArrived?.Invoke(aimTarget);
                    }
                )
                .OnKill(() =>
                    {
                        _lungeTween = null;
                        RestoreRigidbody();

                        if (!_completedNaturally)
                            onCancelled?.Invoke();
                    }
                );
        }

        private float _virtualT;

        private void RestoreRigidbody()
        {
            if (_playerRb == null) return;
            _playerRb.bodyType = _rbTypeBeforeLunge;
            _playerRb = null;
        }

        public void CancelLunge()
        {
            if (_lungeTween != null && _lungeTween.IsActive())
                _lungeTween.Kill(false);
        }

        private bool TryFindTarget(out Vector2 lungeTarget, out Vector2 aimTarget)
        {
            lungeTarget = default;
            aimTarget = default;

            Vector2 origin = _itemComponent._currentOwner.transform.position;
            var hits = Physics2D.OverlapCircle(origin, _lungeAttackComponent.searchRadius, filter, CollidersBuffer);
            if (hits == 0) return false;

            var pointScreenPos = _inputProvider.GetState().Point.ReadValue<Vector2>();
            Vector2 pointPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(pointScreenPos);
            var aimDir = (pointPos - origin).normalized;

            var nearestDist = float.MaxValue;
            var found = false;

            for (var i = 0; i < hits; i++)
            {
                var hit = CollidersBuffer[i];
                var closest = hit.ClosestPoint(origin);
                var toClosest = closest - origin;
                var dist = toClosest.magnitude;

                if (dist > 0.0001f && Vector2.Dot(aimDir, toClosest / dist) < 0.3f) continue;
                if (dist >= nearestDist) continue;

                lungeTarget = closest;
                aimTarget = hit.transform.position;
                nearestDist = dist;
                found = true;
            }

            return found;
        }
    }

    [System.Serializable]
    public class LungeAttackComponent : IComponent
    {
        public float searchRadius = 4f;
        public float stopDistance = 1.2f;
        public float duration = 0.12f;
        public float wallBuffer = 0.15f; 
        public LayerMask enemyLayer,wallLayer;
        public float groundProbeHeight = 2f;
    }
}
