using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Systems;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using Unity.Collections;

namespace Controllers
{
    public abstract class Controller : MonoBehaviour
    {
        public Dictionary<Type, IComponent> Components = new SerializedDictionary<Type, IComponent>();
        public Dictionary<Type, ISystem> Systems = new SerializedDictionary<Type, ISystem>();
        public FieldInfo[] FieldInfos;
        public Action OnUpdate;
        public Action OnFixedUpdate;
        public Action OnLateUpdate;
        public event Action OnGizmosUpdate;
        
        protected virtual void OnValidate() { }
        protected virtual void OnDrawGizmos()
        {
            OnGizmosUpdate?.Invoke();
        }
        protected virtual void Awake()
        {
            EntitySetup();
        }
        
        protected virtual void EntitySetup()
        {
            FieldInfos = GetAllFields(GetType()).ToArray();
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
            foreach (var system in Systems)
            {
                system.Value.Initialize(this);
            }
        }
        
        protected virtual void AddComponentsToList()
        {
            foreach (FieldInfo field in FieldInfos)
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
            foreach (FieldInfo field in  FieldInfos)
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
            Components[component.GetType()] = component;
        }

        public T GetControllerComponent<T>() where T : IComponent
        {
            return Components.ContainsKey(typeof(T)) ? (T)Components[typeof(T)] : default;
        }
        
        public void AddControllerSystem<T>(T system) where T : ISystem
        {
            Systems[system.GetType()] = system;
        }

        public T GetControllerSystem<T>() where T : class, ISystem
        {
            if (Systems.TryGetValue(typeof(T), out var exactMatch))
                return exactMatch as T;
            
            foreach (var system in Systems.Values)
            {
                if (system is T match)
                    return match;
            }

            return null;
        }
        private IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null)
            {
                if(type == typeof(Controller))
                    break;
                foreach (var field in type.GetFields(flags))
                {
                    yield return field;
                }

                type = type.BaseType;
            }
        }
        protected virtual void ReferenceClean()
        {
            
        }
        public virtual void OnDestroy()
        {
            foreach (var sys in Systems.Values)
            {
                if (sys is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            ReferenceClean();
        }
    }
}