using System;
using UnityEngine;
namespace Systems
{
    public class BaseAnimationState : IAnimationState
    {
        protected AnimationStateControllerSystem StateController;

        protected string PlaingAnim;
        public virtual void OnStart(AnimationStateControllerSystem animationStateControllerSystem)
        {
            StateController = animationStateControllerSystem;
        }
        public virtual void OnUpdate()
        {
        }
        public virtual void OnExit()
        {
        }

        public void CrossFade(string name,float time)
        {
            StateController.AnimationStateComponent.animator.CrossFade(name,time);
            PlaingAnim = name;
        }
    }
}