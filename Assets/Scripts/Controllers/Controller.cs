using System;
using System.Reflection;
using Systems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Controllers
{
    public abstract class Controller : MonoBehaviour
    {
        public SerializedDictionary<Type, IComponent> components = new SerializedDictionary<Type, IComponent>();
        public SerializedDictionary<Type, ISystem> systems = new SerializedDictionary<Type, ISystem>();

        public Action OnUpdate;
        public Action OnFixedUpdate;
        public event Action OnGizmosUpdate;
        
        protected virtual void OnValidate()
        {

        }
        protected virtual void OnDrawGizmos()
        {
            OnGizmosUpdate?.Invoke();
        }
        protected virtual void Awake()
        {
            AddComponentsToList();
            AddSystemToList();
            InitSystems();
        }

        public virtual void Update()
        {
            OnUpdate?.Invoke();
        }
        public virtual void FixedUpdate()
        {
            OnFixedUpdate?.Invoke();
        }
        protected virtual void InitSystems()
        {
        }
        
        protected virtual void AddComponentsToList()
        {
        }
        
        protected virtual void AddSystemToList()
        {
        }
        public void AddControllerComponent<T>(T component) where T : IComponent
        {
            components[typeof(T)] = component;
        }

        public T GetControllerComponent<T>() where T : IComponent
        {
            return components.ContainsKey(typeof(T)) ? (T)components[typeof(T)] : default;
        }
        
        public void AddControllerSystem<T>(T system) where T : ISystem
        {
            systems[typeof(T)] = system;
        }

        public T GetControllerSystem<T>() where T : ISystem
        {
            return systems.ContainsKey(typeof(T)) ? (T)systems[typeof(T)] : default;
        }
        
    }
}