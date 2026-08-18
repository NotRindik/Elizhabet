using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class AttackAnimationSystem : BaseSystem, IDisposable
    {
        private AttackAnimationComponent _attackAnim;
        private MeleeComponent _meleeComponent;
        private AnimationComponentsComposer _animation;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _attackAnim = owner.GetControllerComponent<AttackAnimationComponent>();
            _meleeComponent = owner.GetControllerComponent<MeleeComponent>();
            
            ((Item)owner).OnTake += HandleEquip;
            owner.OnUpdate += Update;
        }

        private void HandleEquip(AbstractEntity playerOwner)
        {
            _animation = playerOwner.GetControllerComponent<AnimationComponentsComposer>();
        }

        public override void OnUpdate()
        {
            _attackAnim.Tick(Time.deltaTime);
        }

        public bool BeginAttack()
        {
            if (_attackAnim.CurrentAnimation == null)
                return false;
            
            _animation.SetSpeedOfParts(_meleeComponent.attackSpeed,_attackAnim.partsToLock);
            _animation.UnlockParts(_attackAnim.partsToLock);
            _animation.PlayState(_attackAnim.CurrentAnimation, 0, 0f);
            _animation.LockParts(_attackAnim.partsToLock);

            _attackAnim.Advance();
            return true;
        }
        
        public bool BeginPogoAttack()
        {
            if (_attackAnim.pogoAnim == "")
                return false;

            _animation.SetSpeedOfParts(_meleeComponent.attackSpeed,_attackAnim.partsToLock);
            _animation.UnlockParts(_attackAnim.partsToLock);
            _animation.PlayState(_attackAnim.pogoAnim, 0, 0f);
            _animation.LockParts(_attackAnim.partsToLock);
            return true;
        }

        public void EndAttack()
        {
            _animation.SetSpeedOfParts(1,_attackAnim.partsToLock);
            _animation.UnlockParts(_attackAnim.partsToLock);
            _animation.PlayState("Idle");
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
        public string[] partsToLock = { "LeftHand", "RightHand", "Main" };

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

