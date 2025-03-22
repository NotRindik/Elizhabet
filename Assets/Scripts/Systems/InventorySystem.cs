using System;
using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class InventorySystem : BaseSystem
    {
        InventoryComponent _inventoryComponent;
        BackpackComponent backpackComponent;
        ColorPositioningComponent colorPositioning;

        public void Initialize(Controller owner, InventoryComponent inventoryComponent,BackpackComponent backpackComponent,ColorPositioningComponent colorPositioning)
        {
            base.Initialize(owner);
            this._inventoryComponent = inventoryComponent;
            this.backpackComponent = backpackComponent;
            this.colorPositioning = colorPositioning;
        }

        public override void Update()
        { 
        }

        public void TakeItem(InputAction.CallbackContext callback) 
        {
            Debug.Log("asd");
            Collider2D[] itemCheackZone = Physics2D.OverlapCircleAll(owner.transform.position, _inventoryComponent.itemCheckRadius, _inventoryComponent.itemLayer);
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
                backpackComponent.items.Remove(backpackComponent.ActiveItem);
            }
        }

        public void PreviousItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.currentActiveIndex > 0)
            {
                _inventoryComponent.currentActiveIndex--;
                backpackComponent.SetActiveWeapon(_inventoryComponent.currentActiveIndex);
            }
        }

        public void NextItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.currentActiveIndex < backpackComponent.items.Count - 1)
            {
                _inventoryComponent.currentActiveIndex++;
                backpackComponent.SetActiveWeapon(_inventoryComponent.currentActiveIndex);
            }
        }
    }

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public float itemCheckRadius = 2f;
        public LayerMask itemLayer;
        public Transform handPos;
        public int currentActiveIndex;
    }
}