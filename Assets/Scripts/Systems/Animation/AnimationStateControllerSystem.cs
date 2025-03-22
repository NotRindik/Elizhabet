using Controllers;
using UnityEngine;

namespace Systems
{
    public class AnimationStateControllerSystem : BaseSystem
    {
        private IAnimationState currentState;

        public Controller Controller;

        public Animator animator;

        public override void Initialize(Controller controller)
        {
            Controller = controller;
            animator = controller.GetComponent<Animator>();
            ChangeState(new IdleAnimState());
        }

        public void Update()
        {
            currentState?.OnUpdate();
        }

        public void ChangeState(IAnimationState newState)
        {
            currentState?.OnExit();
            
            currentState = newState;
            
            currentState?.OnStart(this);
        }
    }

    public interface IAnimationState
    {
        public void OnStart(AnimationStateControllerSystem animationStateControllerSystem);

        public void OnUpdate();

        public void OnExit();
    }
}