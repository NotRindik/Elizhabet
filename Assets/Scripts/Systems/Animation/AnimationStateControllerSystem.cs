using System;
using UnityEngine;

namespace Systems
{
    [System.Serializable]
    public class AnimationComponent : IComponent
    {
        public string currentState;

        [SerializeField] private Animator animator;
        public Action<string> OnAnimationStateChange;

        public void SetAnimationSpeed(float speed)
        {
            animator.speed = speed;
        }

        public void CrossFade(string name,float delta)
        {
            currentState = name;
            animator.CrossFade(name, delta);
            OnAnimationStateChange?.Invoke(name);
        }
        
        public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
            currentState = stateName;
            animator.Play(stateName,layer,normalizedTime);
            OnAnimationStateChange?.Invoke(stateName);
        }
    }
}