using Controllers;
using UnityEngine;

namespace Systems
{
    public class BaseSystem :ISystem
    {
        protected Controller owner;
        public bool IsActive { get; set; } = true;
        
        public virtual void Initialize(Controller owner)
        {
            this.owner = owner;
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