using System;
using Controllers;
using Systems;

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

    public InventoryViewComponent InventoryViewComponent = new InventoryViewComponent();

    protected override void Awake()
    {
         base.Awake();
    }


    private void Start()
    {
        _inventoryComponent = player.GetControllerComponent<InventoryComponent>();
        _inventorySystem = player.GetControllerSystem<InventorySystem>();
        InputProvider = player.GetControllerSystem<IInputProvider>();

        AddControllerComponent(_inventoryComponent);
        AddControllerSystem(_inventorySystem);
        AddControllerSystem(InputProvider);

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
                InputProvider.GetState().Move.Enabled = false;
                InputProvider.GetState().Jump.Enabled = false;
                InputProvider.GetState().GrablingHook.Enabled = false;
                InputProvider.GetState().Crouch.Enabled = false;
                InputProvider.GetState().Slide.Enabled = false; 
                InputProvider.GetState().Dash.Enabled = false; 

                animationComponent.CrossFade("BookAppear",0.1f);
                InputProvider.GetState().Back.started += BookOpenCloseHandler;
            }
            else
            {
                InputProvider.GetState().Attack.Enabled = true;
                InputProvider.GetState().Move.Enabled = true;
                InputProvider.GetState().Jump.Enabled = true;
                InputProvider.GetState().GrablingHook.Enabled = true;
                InputProvider.GetState().Crouch.Enabled = true;
                InputProvider.GetState().Slide.Enabled = true;
                InputProvider.GetState().Dash.Enabled = true;

                animationComponent.CrossFade("BookDisAppear",0.1f);
                InputProvider.GetState().Back.started -= BookOpenCloseHandler;
            }
        };

        InputProvider.GetState().Book.started += BookOpenCloseHandler;
    }

    protected override void ReferenceClean()
    {
        base.ReferenceClean();
        InputProvider.GetState().Book.started -= BookOpenCloseHandler;
    }

    public void SetPage(int i)
    {
        _inventorySlotSystem.SetPage(i);
    }

    public void NextPage()
    {
        _inventorySlotSystem.NextPage();
    }
    public void PrevPage()
    {
        _inventorySlotSystem.PrevPage();
    }
}
