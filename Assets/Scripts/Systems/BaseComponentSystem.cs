using Controllers;
using System;
using UnityEngine;

namespace Systems
{
    public class BaseSystem :ISystem
    {
        protected AbstractEntity owner;
        protected MonoBehaviour mono;
        private bool isActive = true;
        public bool IsActive { get => isActive; set { isActive = value; ActiveStateChange?.Invoke(value); } }

        public Transform transform;
        public GameObject gameObject;
        public Action<bool> ActiveStateChange;
        public virtual void Initialize(AbstractEntity owner)
        {
            this.owner = owner;
            mono = (MonoBehaviour)owner;
            transform = mono.transform;
            gameObject = mono.gameObject;
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