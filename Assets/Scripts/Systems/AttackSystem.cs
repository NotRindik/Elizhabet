
using System;
using System.Collections;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class AttackSystem : BaseSystem,IDisposable
    {
        private AttackComponent _attackComponent;

        private SlideComponent _slideComponent;
        private WallRunComponent _wallRunComponent;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private HookComponent _hookComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
            
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _hookComponent = owner.GetControllerComponent<HookComponent>();

            base.owner.OnUpdate += AllowAttack;
        }

        public void AllowAttack()
        {
            _attackComponent.canAttack = _slideComponent.SlideProcess == null &&
                                         _wallRunComponent.wallRunProcess == null &&
                                         _wallEdgeClimbComponent.EdgeStuckProcess == null && !_hookComponent.isHooked;
        }
        public void Dispose()
        {
            base.owner.OnUpdate -= AllowAttack;
        }
    }
    

[System.Serializable]
    public class AttackComponent : IComponent
    {
        public Coroutine AttackProcess;
        public bool isAttackFrame;
        public bool canAttack;

        public void SetAttackFrame(bool val)
        {
            isAttackFrame = val;
        }
    }
}
