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
        private ItemComponent  _itemComponent => owner.GetControllerComponent<ItemComponent>();
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

        public bool TryLungeAttack(Action<Transform> onArrived, Action onCancelled = null)
        {
            if (!TryFindTarget(out Transform target))
                return false;

            LungeTo(target, onArrived, onCancelled);
            return true;
        }

        private Rigidbody2D _playerRb;
        private RigidbodyType2D _rbTypeBeforeLunge;

        public void LungeTo(Transform target, Action<Transform> onArrived, Action onCancelled = null)
        {
            CancelLunge();

            var player = _itemComponent._currentOwner;
            _playerRb = player.GetControllerComponent<ControllersBaseFields>().rb;

            Vector2 start = player.transform.position;
            Vector2 targetPosition = target.position;

            Vector2 direction = (targetPosition - start).normalized;

            if (Vector2.Distance(start, targetPosition) <= _lungeAttackComponent.stopDistance)
            {
                onArrived?.Invoke(target);
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
                    _lungeAttackComponent.duration)
                .SetEase(ease)
                .OnUpdate(() =>
                {
                    Vector2 position = Vector2.Lerp(start, targetPosition, _virtualT);
                    _playerRb.MovePosition(position);
                })
                .OnComplete(() =>
                {
                    _completedNaturally = true;
                    RestoreRigidbody();
                    onArrived?.Invoke(target);
                })
                .OnKill(() =>
                {
                    _lungeTween = null;
                    RestoreRigidbody();

                    if (!_completedNaturally)
                        onCancelled?.Invoke();
                });
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

        private bool TryFindTarget(out Transform target)
        {
            target = null;

            int hits = Physics2D.OverlapCircle(transform.position, _lungeAttackComponent.searchRadius,filter,CollidersBuffer);
            if (hits == 0)
                return false;

            var pointScreenPos = _inputProvider.GetState().Point.ReadValue<Vector2>();
            Vector2 pointPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(pointScreenPos);

            Vector2 pointDir = ((Vector2)transform.position -  pointPos).normalized;
            
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                var hit = CollidersBuffer[i];
                Vector2 enemyToPlayer = (transform.position - hit.transform.position).normalized;

                if (Vector2.Dot(pointDir,enemyToPlayer) < 0.3)
                {
                    continue;
                }
                
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist >= nearestDist)
                    continue;
                
                nearest = hit.transform;
                nearestDist = dist;   
            }
            
            target = nearest;
            return target != null;
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