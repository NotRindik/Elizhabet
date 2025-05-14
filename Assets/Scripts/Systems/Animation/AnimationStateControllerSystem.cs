using UnityEngine;

namespace Systems
{
    [System.Serializable]
    public class AnimationComponent : IComponent
    {
        public string currentState;

        public Animator animator;

        public void CrossFade(string name,float delta)
        {
            currentState = name;
            animator.CrossFade(name, delta);
        }
    }
}