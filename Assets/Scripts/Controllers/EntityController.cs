using System;
using Systems;
using UnityEngine;

namespace Controllers
{
    public abstract class EntityController : Controller
    {
        public ControllersBaseFields baseFields = new ControllersBaseFields();
        protected HealthSystem healthSystem = new HealthSystem();
        public HealthComponent healthComponent = new HealthComponent();
    }
    

    [Serializable]
    public class ControllersBaseFields: IComponent
    {
        public Rigidbody2D rb;
        public Collider2D[] collider;
    }
}