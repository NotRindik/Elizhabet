using System;
using Controllers;
using Systems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class BookController : UIController
{
    private InventorySlotsSystem _inventorySlotSystem;
    
    public IInputProvider InputProvider;

    public AnimationComponent animationComponent;
    private InventoryComponent _inventoryComponent;
    private InventorySystem _inventorySystem;
    public InventorySlotsComponent inventorySlotsComponent;
    
    public Controller player;
    private Action<bool> BookOpenCloseHandler;
    private bool _isBookOpen = false;

    protected override void Awake()
    {
        base.Awake();
    }
    private void Start()
    {
        _inventoryComponent = player.GetControllerComponent<InventoryComponent>();
        _inventorySystem = player.GetControllerSystem<InventorySystem>();
        AddControllerComponent(_inventoryComponent);
        AddControllerSystem(_inventorySystem);
        InputProvider = player.GetControllerSystem<IInputProvider>();
        _inventorySlotSystem = new InventorySlotsSystem();
        _inventorySlotSystem.Initialize(this);
        SubInput();
    }

    public void SubInput()
    {
        BookOpenCloseHandler = c =>
        {
            _isBookOpen = !_isBookOpen;
            if (_isBookOpen)
            {
                InputProvider.GetState().Attack.Enabled = false;
                animationComponent.CrossFade("BookAppear",0.1f);
            }
            else
            {
                InputProvider.GetState().Attack.Enabled = true;
                animationComponent.CrossFade("BookDisAppear",0.1f);
            }
        };
        InputProvider.GetState().Book.started += BookOpenCloseHandler;
    }

    protected override void ReferenceClean()
    {
        base.ReferenceClean();
        InputProvider.GetState().Book.started -= BookOpenCloseHandler;
    }
}
