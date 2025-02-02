using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class TakeThrowItemSystem : BaseSystem
    {
        TakeThrowComponent takeThrowComponent;
        BackpackComponent backpackComponent;
        public void Initialize(Controller owner, TakeThrowComponent takeThrowComponent,BackpackComponent backpackComponent)
        {
            base.Initialize(owner);
            this.takeThrowComponent = takeThrowComponent;
            this.backpackComponent = backpackComponent;
        }

        public override void Update()
        { 
        }

        public void TakeItem(InputAction.CallbackContext callback) 
        {
            Collider2D[] itemCheackZone = Physics2D.OverlapCircleAll(owner.transform.position, takeThrowComponent.itemCheckRadius, takeThrowComponent.itemLayer);
            float nearestDist = float.MaxValue;
            Collider2D nearestItem = null;
            foreach (var item in itemCheackZone)
            {
                var currDistance = Vector2.Distance(item.transform.position, owner.transform.position);
                if (currDistance < nearestDist)
                {
                    nearestDist = currDistance;
                    nearestItem = item;
                }
            }

            if (nearestItem != null)
            {
                var item = nearestItem.GetComponent<Items>();
                backpackComponent.items.Add(item);
                item.TakeUp();
            }
        }
    }

    [System.Serializable]
    public class TakeThrowComponent: IComponent
    {
        public float itemCheckRadius = 2f;
        public LayerMask itemLayer;
    }
}