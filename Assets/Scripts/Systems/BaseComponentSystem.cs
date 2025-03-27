using Controllers;
using UnityEngine;

namespace Systems
{
    public class BaseSystem : ISystem
    {
        protected Controller owner;
        
        public virtual void Initialize(Controller owner)
        {
            this.owner = owner;
        }
        public virtual void Update()
        {
        }
    }
}