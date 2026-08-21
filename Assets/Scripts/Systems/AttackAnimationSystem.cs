using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class AttackAnimationSystem : BaseSystem, IDisposable
    {
        private AttackAnimationComponent _attackAnim;
        private AttackComponent _attackComponent;
        private MeleeComponent _meleeComponent;
        private AnimationComponentsComposer _animation;

        private bool _isAttackAnim;

        public Action OnAnimEnd;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _attackAnim = owner.GetControllerComponent<AttackAnimationComponent>();
            _meleeComponent = owner.GetControllerComponent<MeleeComponent>();
            
            ((Item)owner).OnTake += HandleEquip;
            owner.OnLateUpdate += Update;
        }


        private void HandleEquip(AbstractEntity playerOwner)
        {
            _animation = playerOwner.GetControllerComponent<AnimationComponentsComposer>();
            _attackComponent = playerOwner.GetControllerComponent<AttackComponent>();
        }

        public override void OnUpdate()
        {
            _attackAnim.Tick(Time.deltaTime);

            if (_isAttackAnim)
            {
                var progress = _animation.GetLockedProgressOfStateRaw(_playingState);
                if (progress >= 0.9)
                {
                    OnAnimEnd?.Invoke();
                    _isAttackAnim = false;
                }
            }
        }

        private string _playingState;

        public bool BeginAttack()
        {
            if (_attackAnim.CurrentAnimation == null) return false;
            if (_isAttackAnim) return false;

            _playingState = _attackAnim.CurrentAnimation;

            _animation.SetSpeedOfParts(_meleeComponent.attackSpeed, _attackAnim.partsToLock);
            _animation.UnlockParts(_attackAnim.partsToLock);
            _animation.PlayState(_playingState, 0, 0f);
            _animation.LockParts(_attackAnim.partsToLock);
            _isAttackAnim = true;
            _attackComponent.isAttackAnim = true;

            _attackAnim.Advance();
            return true;
        }
        
        public bool BeginPogoAttack()
        {
            if (_attackAnim.pogoAnim == "") return false;
            if (_isAttackAnim) return false;

            _playingState = _attackAnim.pogoAnim;

            _animation.SetSpeedOfParts(_meleeComponent.attackSpeed, _attackAnim.partsToLock);
            _animation.UnlockParts(_attackAnim.partsToLock);
            _animation.PlayState(_playingState, 0, 0f);
            _animation.LockParts(_attackAnim.partsToLock);
            _isAttackAnim = true;
            _attackComponent.isAttackAnim = true;
            return true;
        }

        public void EndAttack()
        {
            _animation.SetSpeedOfParts(1,_attackAnim.partsToLock);
            _animation.UnlockParts(_attackAnim.partsToLock);
            _animation.PlayState("Idle");
            _attackComponent.isAttackAnim = false;
        }

        public void Dispose()
        {
            owner.OnUpdate -= OnUpdate;
        }
    }
    
    
    [System.Serializable]
    public class AttackAnimationComponent : IComponent
    {
        public string[] combo;
        public string pogoAnim;
        public float comboResetTime = 0.6f;
        public string[] partsToLock = { "LeftPivot", "RightHand", "& Eizhabethth" };

        private int _index;
        private float _resetTimer;
        private bool _timerActive;

        public string CurrentAnimation =>
            combo != null && combo.Length > 0 ? combo[_index] : null;

        public void Advance()
        {
            if (combo == null || combo.Length == 0)
                return;

            _index = (_index + 1) % combo.Length;
            _resetTimer = comboResetTime;
            _timerActive = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_timerActive)
                return;

            _resetTimer -= deltaTime;
            if (_resetTimer <= 0f)
                ResetCombo();
        }

        public void ResetCombo()
        {
            _index = 0;
            _timerActive = false;
        }
    }

}

