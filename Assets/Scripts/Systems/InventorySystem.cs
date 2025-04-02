using System;
using System.Collections.Generic;
using Assets.Scripts;
using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class InventorySystem : BaseSystem
    {
        InventoryComponent _inventoryComponent;
        ColorPositioningComponent colorPositioning;
        private EntityController owner;

        public void Initialize(Controller owner, InventoryComponent inventoryComponent,ColorPositioningComponent colorPositioning)
        {
            base.Initialize(owner);
            this.owner = (EntityController)owner;
            this._inventoryComponent = inventoryComponent;
            this.colorPositioning = colorPositioning;
        }

        public override void Update()
        { 
        }

        public void TakeItem(InputAction.CallbackContext callback) 
        {
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

                if (_inventoryComponent.items.Count == 0)
                {
                    item.TakeUp(colorPositioning, owner);
                    _inventoryComponent.ActiveItem = item;
                    _inventoryComponent.ActiveItem.itemComponent.currentOwner = owner;
                }
                else
                {
                    MonoBehaviour.Destroy(item.gameObject);
                }
                _inventoryComponent.items.Add(item.itemComponent);
            }
        }

        public void ThrowItem(InputAction.CallbackContext callback)
        {
            if (_inventoryComponent.items.Count > 0)
            {
                _inventoryComponent.ActiveItem.Throw();
                
                _inventoryComponent.items.Remove(_inventoryComponent.ActiveItem.itemComponent);
                _inventoryComponent.ActiveItem = null;
                if (_inventoryComponent.items.Count > 0)
                {
                    SetActiveWeaponWithoutDestroy(_inventoryComponent.items.Count - 1);
                }
            }
        }

        public void PreviousItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.currentActiveIndex > 0)
            {
                _inventoryComponent.currentActiveIndex--;
                SetActiveWeapon(_inventoryComponent.currentActiveIndex);
            }
        }

        public void NextItem(InputAction.CallbackContext callbackContext)
        {
            if (_inventoryComponent.currentActiveIndex < _inventoryComponent.items.Count - 1)
            {
                _inventoryComponent.currentActiveIndex++;
                SetActiveWeapon(_inventoryComponent.currentActiveIndex);
            }
        }

        public void SetActiveWeapon(int index)
        {
            GameObject.Destroy(_inventoryComponent.ActiveItem.gameObject);
            SetActiveWeaponWithoutDestroy(index);
        }
        public void SetActiveWeaponWithoutDestroy(int index)
        {
            GameObject inst = (GameObject)GameObject.Instantiate(_inventoryComponent.items[index].itemPrefab);
            _inventoryComponent.ActiveItem = inst.GetComponent<Items>();
            _inventoryComponent.ActiveItem.itemComponent = _inventoryComponent.items[index];
            _inventoryComponent.ActiveItem.TakeUp(colorPositioning, owner);
            _inventoryComponent.ActiveItem.itemComponent.currentOwner = owner;
        }
    }

    [System.Serializable]
    public class InventoryComponent: IComponent
    {
        public float itemCheckRadius = 2f;
        public LayerMask itemLayer;
        public int currentActiveIndex;

        public List<ItemComponent> items = new List<ItemComponent>();

        public Action<Items> OnActiveItemChange;

        private Items _activeItem;

        public Items ActiveItem
        {
            get
            {
                return _activeItem;
            }

            set
            {
                _activeItem = value;
                OnActiveItemChange?.Invoke(_activeItem);
            }
        }
    }
}