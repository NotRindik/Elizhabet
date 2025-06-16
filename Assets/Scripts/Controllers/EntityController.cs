using System;
using Systems;
using UnityEngine;

namespace Controllers
{
    public abstract class EntityController : Controller
    {
        public ControllersBaseFields baseFields;
        protected HealthSystem healthSystem = new HealthSystem();
        public HealthComponent healthComponent = new HealthComponent();
        
        protected virtual void OnValidate()
        {
            if (baseFields.collider == null)
            {
                baseFields.collider = GetComponent<Collider2D>();
            }
            
            if (baseFields.rb == null)
            {
                baseFields.rb = GetComponent<Rigidbody2D>();
            }
        }
    }
    

    [Serializable]
    public class ControllersBaseFields: IComponent
    {
        public Rigidbody2D rb;
        public Collider2D collider;
    }
}