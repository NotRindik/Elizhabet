using Controllers;
using UnityEngine;

namespace Systems
{
    public class AnimationStateControllerSystem : BaseSystem
    {
        public AnimationStateComponent AnimationStateComponent;
        public Controller Controller;
        
        public override void Initialize(Controller controller)
        {
            Controller = controller;
            AnimationStateComponent = controller.GetControllerComponent<AnimationStateComponent>();
            AnimationStateComponent.animator = controller.GetComponent<Animator>();
            ChangeState(new IdleAnimState());
        }

        public void Update()
        {
            AnimationStateComponent.currentState?.OnUpdate();
        }

        public void ChangeState(IAnimationState newState)
        {
            AnimationStateComponent.currentState?.OnExit();
            
            AnimationStateComponent.currentState = newState;
            
            AnimationStateComponent.currentState?.OnStart(this);
        }
    }

    public class AnimationStateComponent : IComponent
    {
        public IAnimationState currentState;

        public Animator animator;
    }

    public interface IAnimationState
    {
        public void OnStart(AnimationStateControllerSystem animationStateControllerSystem);

        public void OnUpdate();

        public void OnExit();
    }
}