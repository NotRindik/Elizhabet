using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    [System.Serializable]
    public class AttackAnimationSystem : BaseSystem, IDisposable
    {
        private AttackAnimationComponent _attackAnim;
        private AttackComponent _attackComponent;
        private MeleeComponent _meleeComponent;
        private AnimationComponentsComposer _animation;

        private bool _isAttackAnim
        {
            get => _attackAnim.isAttackAnim;
            set
            {
                _attackAnim.isAttackAnim = value;
            }
        }

        public Action OnAnimEnd;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _attackAnim = owner.GetControllerComponent<AttackAnimationComponent>();
            _meleeComponent = owner.GetControllerComponent<MeleeComponent>();
            
            ((Item)owner).OnTake += HandleEquip;
            ((Item)owner).OnReferenceClean += HandleUnequip;
            owner.OnLateUpdate += Update;
        }


        private void HandleEquip(AbstractEntity playerOwner)
        {
            _animation = playerOwner.GetControllerComponent<AnimationComponentsComposer>();
            _attackComponent = playerOwner.GetControllerComponent<AttackComponent>();
        }

        private void HandleUnequip()
        {
            if (_animation != null && _attackComponent != null && _attackComponent.actionAnimation != null && _isAttackAnim)
                EndAttack();
        }

        public override void OnUpdate()
        {
            _attackAnim.Tick(Time.deltaTime);
            
            if (_isAttackAnim)
            {
                var progress = _animation.GetLayerStateProgressRaw("Action", _playingState);
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

            _animation.SetSpeed("Attack", _meleeComponent.attackSpeed, _attackAnim.partsToLock);
            _attackComponent.actionAnimation = _playingState;
            _animation.Tick();
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

            _animation.SetSpeed("Attack", _meleeComponent.attackSpeed, _attackAnim.partsToLock);
            _attackComponent.actionAnimation = _playingState;
            _animation.Tick();
            _isAttackAnim = true;
            _attackComponent.isAttackAnim = true;
            return true;
        }

        public void EndAttack()
        {
            _animation.ClearSpeed("Attack");
            _attackComponent.actionAnimation = null;
            _animation.Tick();
            _attackComponent.isAttackAnim = false;
            _isAttackAnim = false;
        }

        public void Dispose()
        {
            owner.OnLateUpdate -= Update;
            ((Item)owner).OnTake -= HandleEquip;
            ((Item)owner).OnReferenceClean -= HandleUnequip;
        }
    }
    
    
    [System.Serializable]
    public class AttackAnimationComponent : IComponent
    {
        public string[] combo;
        public string pogoAnim;
        public float comboResetTime = 0.6f;
        public string[] partsToLock = { "LeftPivot", "RightHand", "& Eizhabethth" };
        public bool isAttackAnim;
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

