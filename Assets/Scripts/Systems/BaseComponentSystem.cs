using Controllers;
using System;
using UnityEngine;

namespace Systems
{
    public class BaseSystem :ISystem
    {
        protected AbstractEntity owner;
        protected MonoBehaviour mono;
        protected bool isActive = true;
        protected bool WasInitialized;

        public virtual bool IsActive
        {
            get => isActive;
            set
            {
                if (value == true)
                    OnEnable();
                else
                    OnDisable();
                
                isActive = value;
                OnActiveStateChange(value);
            }
        }

        public Transform transform;
        public GameObject gameObject;
        public Action<bool> ActiveStateChange;
        protected virtual void OnActiveStateChange(bool value)
        {
            ActiveStateChange?.Invoke(WasInitialized);
        } 
        public virtual void Initialize(AbstractEntity owner)
        {
            this.owner = owner;
            mono = (MonoBehaviour)owner;
            transform = mono.transform;
            gameObject = mono.gameObject;
            WasInitialized = true;
        }

        public virtual void OnEnable()
        {
            
        }

        public virtual void OnDisable()
        {
            
        }

        public virtual void Update()
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