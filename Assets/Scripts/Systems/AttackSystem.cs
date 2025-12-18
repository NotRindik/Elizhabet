
using System;
using System.Collections;
using System.Collections.Generic;
using Controllers;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems
{
    public class AttackSystem : BaseSystem,IDisposable
    {
        protected AttackComponent _attackComponent;

        private SlideComponent _slideComponent;
        private WallRunComponent _wallRunComponent;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private HookComponent _hookComponent;
        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
            
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _hookComponent = owner.GetControllerComponent<HookComponent>();

            base.owner.OnUpdate += AllowAttack;
        }

        public virtual void AllowAttack()
        {
            _attackComponent.canAttack = _slideComponent.SlideProcess == null &&
                                         _wallRunComponent.wallRunProcess == null &&
                                         _wallEdgeClimbComponent.EdgeStuckProcess == null && !_hookComponent.isHooked
                                         && _attackComponent.AttackProcess == null;
        }
        public void Dispose()
        {
            base.owner.OnUpdate -= AllowAttack;
        }
    }
    

[System.Serializable]
    public unsafe class AttackComponent : IComponent
    {
        public Coroutine AttackProcess;

        private bool _isAttackFrame;
        public bool isAttackFrame
        {
            get => _isAttackFrame;
            set
            {
                _isAttackFrame = value;
                if(value == true)
                    OnAttackStart?.Invoke();
                else
                {
                    OnAttackEnd?.Invoke();
                }
            }
        }
        public bool canAttack;
        public bool isAttackFrameThisFrame;

        public bool isAttackAnim;

        public Action OnAttackStart;
        public Action OnAttackEnd;

        public ObservableList<IntPtr> damageModifire = new();

        public void SetAttackFrame(bool val)
        {
            isAttackFrame = val;
        }
    }
}
