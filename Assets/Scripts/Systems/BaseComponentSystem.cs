using Controllers;
using System;
using UnityEngine;

namespace Systems
{
    public class BaseSystem :ISystem
    {
        protected Controller owner;
        private bool isActive = true;
        public bool IsActive { get => isActive; set { isActive = value; ActiveStateChange?.Invoke(value); } }

        public Transform transform;
        public GameObject gameObject;
        public Action<bool> ActiveStateChange;
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