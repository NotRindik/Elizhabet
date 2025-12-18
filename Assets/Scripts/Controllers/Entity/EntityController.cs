using System;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class EntityController : Controller
    {
        [Header("Basic")]
        public ControllersBaseFields baseFields = new ControllersBaseFields();
        public HealthComponent healthComponent = new HealthComponent();
        protected HealthSystem healthSystem = new HealthSystem();
        public Action<EntityController> OnRequestDestroy;
        public Action<Collision2D> OnCollisionEnter2DHandle;
        private void Start()
        {
            healthComponent.OnDie += OnDie;
        }

        public virtual void OnCollisionEnter2D(Collision2D other)
        {
            OnCollisionEnter2DHandle?.Invoke(other);
        }

        public virtual void OnDie(IController controller)
        {
            Destroy(controller.mono.gameObject);
        }

        protected override void ReferenceClean()
        {
            base.ReferenceClean();
            healthComponent.OnDie -= OnDie;
        }
    }
    

    [Serializable]
    public class ControllersBaseFields: IComponent
    {
        public Rigidbody2D rb;
        public Collider2D[] collider;
    }
}