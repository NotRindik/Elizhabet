using Controllers;
using UnityEngine;

namespace Systems
{
    public class BaseSystem :ISystem
    {
        protected Controller owner;
        public bool IsActive { get; set; } = true;

        public Transform transform;
        public GameObject gameObject;
        
        public virtual void Initialize(Controller owner)
        {
            this.owner = owner;
            transform = owner.transform;
            gameObject = owner.gameObject;
        }
        
        public void Update()
        {
            if (!IsActive)
                return;

            OnUpdate();
        }
        public virtual void OnUpdate()
        {
            
        }
    }
}