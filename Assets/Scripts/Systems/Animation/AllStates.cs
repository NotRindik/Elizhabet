using Controllers;
using UnityEngine;
namespace Systems
{
    public class IdleAnimState : BaseAnimationState
    {
        protected MoveComponent MoveComponent;
        protected JumpComponent JumpComponent;
        protected AttackComponent AttackComponentComponent;

        public override void OnStart(AnimationStateControllerSystem animationStateControllerSystem)
        {
            base.OnStart(animationStateControllerSystem);
            MoveComponent = StateController.Controller.GetControllerComponent<MoveComponent>();
            JumpComponent = StateController.Controller.GetControllerComponent<JumpComponent>();
            AttackComponentComponent = StateController.Controller.GetControllerComponent<AttackComponent>();
            
            CrossFade("Idle",0.1f);
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (AttackComponentComponent.AttackProcess != null)
            {
                StateController.ChangeState(new OneHandAttack());
            }
                
            if (MoveComponent.direction != Vector2.zero)
            {
                if (StateController.Controller.baseFields.rb.linearVelocityX >= 0.5f || StateController.Controller.baseFields.rb.linearVelocityX <= -0.5f)
                {
                    StateController.ChangeState(new MoveAnimState());
                }
            }
            if (JumpComponent.isGround == false)
            {
                StateController.ChangeState(new FallAnimState());
            }
        }
    }
    public class OneHandAttack : BaseAnimationState
    {
        private AttackComponent _attackComponent;
        private MoveComponent _moveComponent;
        private SpriteFlipComponent flipComponent;

        Vector2 tempOfDir;
        public override void OnStart(AnimationStateControllerSystem animationStateControllerSystem)
        {
            base.OnStart(animationStateControllerSystem);
            _attackComponent = StateController.Controller.GetControllerComponent<AttackComponent>();
            _moveComponent = StateController.Controller.GetControllerComponent<MoveComponent>();
            _moveComponent.speedMultiplierDynamic = 0;
            flipComponent = StateController.Controller.GetControllerComponent<SpriteFlipComponent>();
            tempOfDir = flipComponent.direction;

            CrossFade("OneArmed_AttackForward", 0.1f);
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
            flipComponent.direction = tempOfDir;

            if (_attackComponent.AttackProcess == null)
            {
                _moveComponent.speedMultiplierDynamic = 1;
                StateController.ChangeState(new IdleAnimState());
            }
        }
    }
    
    public class MoveAnimState : BaseAnimationState
    {
        protected MoveComponent moveComponent;
        protected JumpComponent jumpComponent;
        protected AttackComponent AttackComponentComponent;

        public override void OnStart(AnimationStateControllerSystem animationStateControllerSystem)
        {
            base.OnStart(animationStateControllerSystem);
            moveComponent = animationStateControllerSystem.Controller.GetControllerComponent<MoveComponent>();
            jumpComponent = animationStateControllerSystem.Controller.GetControllerComponent<JumpComponent>();
            AttackComponentComponent = StateController.Controller.GetControllerComponent<AttackComponent>();
            CrossFade("Walk",0.1f);
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (AttackComponentComponent.AttackProcess != null)
            {
                StateController.ChangeState(new OneHandAttack());
            }
                            
            if (jumpComponent.isGround == false)
            {
                StateController.ChangeState(new FallAnimState());
            }
            
            if (moveComponent.direction == Vector2.zero || (StateController.Controller.baseFields.rb.linearVelocityX > -0.5f && StateController.Controller.baseFields.rb.linearVelocityX < 0.5f))
            {
                StateController.ChangeState(new IdleAnimState());
            }
        }
    }
    
    public class FallAnimState : BaseAnimationState
    {
        protected MoveComponent moveComponent;
        protected JumpComponent jumpComponent;
        protected AttackComponent AttackComponentComponent;
        public override void OnStart(AnimationStateControllerSystem animationStateControllerSystem)
        {
            base.OnStart(animationStateControllerSystem);
            moveComponent = animationStateControllerSystem.Controller.GetControllerComponent<MoveComponent>();
            jumpComponent = animationStateControllerSystem.Controller.GetControllerComponent<JumpComponent>();
            AttackComponentComponent = StateController.Controller.GetControllerComponent<AttackComponent>();
            CrossFade("FallUp",0.1f);
        }
        public override void OnUpdate()
        {
            base.OnUpdate();

            if (AttackComponentComponent.AttackProcess != null)
            {
                StateController.ChangeState(new OneHandAttack());
            }

            if (jumpComponent.isGround)
            {
                
                if (moveComponent.direction == Vector2.zero)
                {
                    StateController.ChangeState(new IdleAnimState());
                }
                else
                {
                    StateController.ChangeState(new MoveAnimState());
                }
            }
            else
            {
                if (StateController.Controller.baseFields.rb.linearVelocityY > 0.5f)
                {
                    CrossFade("FallUp",0.1f);
                }
                else if(StateController.Controller.baseFields.rb.linearVelocityY > -0.5f)
                {
                    CrossFade("FallDown",0.1f);
                }
            }
        }
    }
}