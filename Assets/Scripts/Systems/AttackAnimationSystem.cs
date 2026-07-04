using UnityEngine;

namespace Systems
{
    public class AttackAnimationSystem : BaseSystem
    {
        private AttackAnimationComponent _attackAnim;
        private AnimationComponentsComposer _animation;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _attackAnim = owner.GetControllerComponent<AttackAnimationComponent>();
            _animation = owner.GetControllerComponent<AnimationComponentsComposer>();
            owner.OnUpdate += OnUpdate;
        }

        public override void OnUpdate()
        {
            _attackAnim.Tick(Time.deltaTime);
        }
        
        public bool PlayNextAttack()
        {
            if (_attackAnim.CurrentAnimation == null)
                return false;

            _animation.UnlockParts(_attackAnim.partsToLock);
            _animation.PlayState(_attackAnim.CurrentAnimation, 0, 0f);
            _animation.LockParts(_attackAnim.partsToLock);

            _attackAnim.Advance();
            return true;
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

