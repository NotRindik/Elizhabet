using System;
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
        protected HealthSystem healthSystem = new HealthSystem();
        protected Stats stats = new Stats();
        public HealthComponent healthComponent = new HealthComponent();
        
        protected virtual void OnValidate()
        {

        }
        protected virtual void Start()
        {
            AddComponentsToList();
            AddSystemToList();
            InitSystems();
        }
        protected virtual void InitSystems()
        {
            healthSystem.Initialize(this);
        }
        
        protected virtual void AddComponentsToList()
        {
            AddControllerComponent(stats);
            AddControllerComponent(healthComponent);
        }
        
        protected virtual void AddSystemToList()
        {
            AddControllerSystem(healthSystem);
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