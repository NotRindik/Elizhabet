using System;
using System.Collections.Generic;
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
        public Action OnLateUpdate;
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

        public virtual void LateUpdate()
        {
            OnLateUpdate?.Invoke();
        }
        protected virtual void InitSystems()
        {
            foreach (var system in systems)
            {
                system.Value.Initialize(this);
            }
        }
        
        protected virtual void AddComponentsToList()
        {
            foreach (FieldInfo field in  GetAllFields(GetType()))
            {
                if (typeof(IComponent).IsAssignableFrom(field.FieldType))
                {
                    IComponent fieldValue = (IComponent)field.GetValue(this);
                    if (fieldValue == null)
                    {
                        continue;
                    }
                    AddControllerComponent(fieldValue);
                }
            }
        }
        
        protected virtual void AddSystemToList()
        {
            foreach (FieldInfo field in  GetAllFields(GetType()))
            {
                if (typeof(ISystem).IsAssignableFrom(field.FieldType))
                {
                    ISystem fieldValue = (ISystem)field.GetValue(this);
                    if (fieldValue == null)
                    {
                        continue;
                    }
                    AddControllerSystem(fieldValue);
                }
            }
        }
        public void AddControllerComponent<T>(T component) where T : IComponent
        {
            components[component.GetType()] = component;
        }

        public T GetControllerComponent<T>() where T : IComponent
        {
            return components.ContainsKey(typeof(T)) ? (T)components[typeof(T)] : default;
        }
        
        public void AddControllerSystem<T>(T system) where T : ISystem
        {
            systems[system.GetType()] = system;
        }

        public T GetControllerSystem<T>() where T : ISystem
        {
            return systems.ContainsKey(typeof(T)) ? (T)systems[typeof(T)] : default;
        }
        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null)
            {
                foreach (var field in type.GetFields(flags))
                    yield return field;

                type = type.BaseType;
            }
        }
    }
}