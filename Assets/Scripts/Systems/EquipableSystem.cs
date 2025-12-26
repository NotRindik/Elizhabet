using Controllers;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems
{
    interface ITakeAbleSystem : ISystem
    {
        public void SelectItem(AbstractEntity owner);
        public void Throw(Vector2 dir = default, float force = 15);
        public void Throw() { }
        public bool isSelected {  get; set; }
    }
    public class TakeAbleSystem : BaseSystem, ITakeAbleSystem
    {
        private ControllersBaseFields _baseFields;
        private ItemComponent _itemC;

        public bool isSelected { get ; set ;}

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _itemC = owner.GetControllerComponent<ItemComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
        }

        public void SelectItem(AbstractEntity owner)
        {
            if (!IsActive)
                return;

            _itemC.currentOwner = owner;
            _baseFields.rb.bodyType = RigidbodyType2D.Static;

            foreach (var col in _baseFields.collider)
            {
                col.isTrigger = true;
            }
        }

        public void Throw(Vector2 dir = default, float force = 15)
        {
            throw new System.NotImplementedException();
        }
    }
}