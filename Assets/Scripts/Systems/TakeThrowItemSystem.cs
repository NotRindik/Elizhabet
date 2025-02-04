using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class TakeThrowItemSystem : BaseSystem
    {
        TakeThrowComponent takeThrowComponent;
        BackpackComponent backpackComponent;
        ColorPositioningComponent colorPositioning;

        public void Initialize(Controller owner, TakeThrowComponent takeThrowComponent,BackpackComponent backpackComponent,ColorPositioningComponent colorPositioning)
        {
            base.Initialize(owner);
            this.takeThrowComponent = takeThrowComponent;
            this.backpackComponent = backpackComponent;
            this.colorPositioning = colorPositioning;
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
                var itemComponent = item.ItemComponent;

                if (backpackComponent.items.Count == 0)
                {
                    item.TakeUp(colorPositioning, owner);
                }
                else
                {
                    MonoBehaviour.Destroy(item.gameObject);
                }
                backpackComponent.items.Add(item.ItemComponent);
            }
        }

        public void ThrowItem(InputAction.CallbackContext callback)
        {
            if (backpackComponent.items.Count > 0)
            {
                //backpackComponent.items[backpackComponent.currentItem].Throw();
                backpackComponent.items.Remove(backpackComponent.items[backpackComponent.currentItem]);
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