using System.Collections;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class PlatformSystem: BaseSystem
    {
        private PlatformComponent _platformComponent;
        private GroundingComponent _groundingComponent;
        private ControllersBaseFields _baseFields;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _platformComponent = owner.GetControllerComponent<PlatformComponent>();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            foreach (var col in _groundingComponent.groundedColliders)
            {
                if (col.TryGetComponent(out PlatformEffector2D _))
                {
                    _platformComponent.IgnoreProcess = owner.StartCoroutine(IgnoreCollisionProcess(col));
                }
            }
        }

        private IEnumerator IgnoreCollisionProcess(Collider2D col)
        {
            foreach (var entityCol in _baseFields.collider)
            {
                Physics2D.IgnoreCollision(col,entityCol,true);   
            }
            yield return new WaitForSeconds(_platformComponent.unCollisionTime);
            foreach (var entityCol in _baseFields.collider)
            {
                Physics2D.IgnoreCollision(col,entityCol,false);   
            }
            _platformComponent.IgnoreProcess = null;
        } 
    }

    [System.Serializable]
    public class PlatformComponent : IComponent
    {
        public float unCollisionTime = 0.2f;
        public Coroutine IgnoreProcess;
    }
}