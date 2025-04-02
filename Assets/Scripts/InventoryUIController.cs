using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Controllers
{
    public class InventoryUIController : UIController
    {
        public PlayerController playerController;

        private InventoryComponent _inventoryComponent;
        private HolderSystem _holderSystem = new HolderSystem();

        public HolderComponent holderComponent = new HolderComponent();

        protected void Start()
        {
            _inventoryComponent = playerController.GetControllerComponent<InventoryComponent>();
            Debug.Log(_inventoryComponent);
            AddControllerComponent(_inventoryComponent);
            _holderSystem.Initialize(this);
        }

        protected override void InitSystems()
        {
            base.InitSystems();
        }

        protected override void AddComponentsToList()
        {
            base.AddComponentsToList();
            AddControllerComponent(holderComponent);
        }
    
        protected override void AddSystemToList()
        {
            base.AddComponentsToList();
            AddControllerSystem(_holderSystem);
        }
    }
   
}
namespace Systems
{
    using UnityEngine.UI;
    public class HolderSystem: BaseSystem
    {
        private HolderComponent _holderComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            var inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _holderComponent = owner.GetControllerComponent<HolderComponent>();
            inventoryComponent.OnActiveItemChange += Update;
            Update();
        }

        public new  void Update(Items activeItem)
        {
            if (activeItem == null)
            {
                _holderComponent.itemHolder.color = new Color(0, 0, 0, 0);
            }
            else
            {
                _holderComponent.itemHolder.sprite = activeItem.itemComponent.itemIcon;
                _holderComponent.itemHolder.color = new Color(1, 1, 1, 1);
            }
            base.Update();
        }
    }  
    
    [System.Serializable]
    public class HolderComponent: IComponent
    {
        public Image itemHolder;
    }   
}
