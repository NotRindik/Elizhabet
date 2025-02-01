using System;
using UnityEngine;
namespace Systems
{
    public class BaseAnimationSystem : BaseSystem
    {
        protected AnimationComponent animationComponent;
        public virtual void Initialize(AnimationComponent animationComponent)
        {
            this.animationComponent = animationComponent;
        }
        public override void Update()
        {
            StateLogic();
        }
        protected virtual void StateLogic()
        {
            var state = GetState();

            if (state == animationComponent.currentState) return;
            animationComponent.anim.CrossFade(state, 0.1f, 0);
            animationComponent.currentState = state;
        }
        public virtual string GetState()
        {
            return "Idle";

            int LockState(int s, float t)
            {
                animationComponent.lockedTill = Time.time + t;
                return s;
            }
        }
    }

    [System.Serializable]
    public class AnimationComponent: IComponent
    {
        internal string currentState;
        internal float lockedTill;
        internal Animator anim;
        internal Rigidbody2D rigidbody;
    }

    public interface IAnimationController
    {
        public void AnimationSystemsWeights() { 
        }
    }
}