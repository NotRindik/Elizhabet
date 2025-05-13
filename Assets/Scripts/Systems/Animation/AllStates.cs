/*using Assets.Scripts;
using Controllers;
using UnityEngine;
using UnityEngine.Windows;
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
                if (StateController.Controller.baseFields.rb.linearVelocityX is >= 0.5f or <= -0.5f)
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
        private AttackSystem _attackSystem;
        private MoveComponent _moveComponent;
        private SpriteFlipComponent flipComponent;
        private bool isRepeat;

        Vector2 tempOfDir;
        public override void OnStart(AnimationStateControllerSystem animationStateControllerSystem)
        {
            AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Замах");
            base.OnStart(animationStateControllerSystem);
            _attackComponent = StateController.Controller.GetControllerComponent<AttackComponent>();
            _moveComponent = StateController.Controller.GetControllerComponent<MoveComponent>();
            _attackSystem = StateController.Controller.GetControllerSystem<AttackSystem>();
            if (StateController.Controller.baseFields.rb.linearVelocityY < 0.1f && StateController.Controller.baseFields.rb.linearVelocityY > - 0.1f)
            {
                _moveComponent.speedMultiplierDynamic = 0;
            }
            flipComponent = StateController.Controller.GetControllerComponent<SpriteFlipComponent>();
            tempOfDir = flipComponent.direction;
            var inputState = ((PlayerController)StateController.Controller).input.GetState();
            inputState.inputActions.Player.Next.Disable();
            inputState.inputActions.Player.Previous.Disable();
            inputState.inputActions.Player.Jump.Disable();
            inputState.inputActions.Player.Interact.Disable();
            animationStateControllerSystem.AnimationStateComponent.animator.Play("OneArmed_AttackForward",-1,0f);
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
            flipComponent.direction = tempOfDir;

            AnimatorStateInfo stateInfo = StateController.AnimationStateComponent.animator.GetCurrentAnimatorStateInfo(0);
            
            if (StateController.Controller.baseFields.rb.linearVelocityY < 0.1f && StateController.Controller.baseFields.rb.linearVelocityY > - 0.1f)
            {
                _moveComponent.speedMultiplierDynamic = 0;
            }
            
            if (stateInfo.IsName("OneArmed_AttackForward") && stateInfo.normalizedTime >= 1.0f)
            {
                var inputState = ((PlayerController)StateController.Controller).input.GetState();
                if (inputState.inputActions.Player.Attack.ReadValue<float>() > 0)
                {
                    isRepeat = true;
                }
                
                if (!isRepeat)
                {
                    if (_attackComponent.AttackProcess != null)
                    {
                        StateController.ChangeState(new IdleAnimState());   
                    }
                }
                else
                {else
                    if(_attackComponent.AttackProcess != null)
                        StateController.Controller.StopCoroutine(_attackComponent.AttackProcess);
                    _attackComponent.AttackProcess = null;
                    _attackSystem.OnUpdate();
                    StateController.ChangeState(new OneHandAttack());
                }
            }
        }

        public override void OnExit()
        {
            ((PlayerController)StateController.Controller).input.GetState().inputActions.Player.Next.Enable();
            ((PlayerController)StateController.Controller).input.GetState().inputActions.Player.Previous.Enable();
            ((PlayerController)StateController.Controller).input.GetState().inputActions.Player.Jump.Enable();
            ((PlayerController)StateController.Controller).input.GetState().inputActions.Player.OnDrop.Enable();
            ((PlayerController)StateController.Controller).input.GetState().inputActions.Player.Interact.Enable();
            _moveComponent.speedMultiplierDynamic = 1;
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
            
            if (moveComponent.direction == Vector2.zero || StateController.Controller.baseFields.rb.linearVelocityX is > -0.2f and < 0.2f)
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
                return;
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
}*/