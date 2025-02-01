using System;
using Systems;
using UnityEngine;
using UnityEngine.Rendering;

namespace Controllers
{
    public abstract class Controller : MonoBehaviour
    {
        public SerializedDictionary<Type, IComponent> components = new SerializedDictionary<Type, IComponent>();
        public ControllersBaseFields baseFields;
        protected virtual void OnValidate()
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
        [Serializable]
        public class ControllersBaseFields
        {
            public Rigidbody2D rb;
            public Collider2D collider;
        }
    }
}