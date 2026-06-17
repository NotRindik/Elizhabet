using System;
using Controllers;
using Systems;
using TMPro;
using UnityEngine;

public class BookController : UIController
{
    private InventorySlotsSystem _inventorySlotSystem;
    private HealthComponent _healthComponent;
    private ProtectionComponent _protectionComponent;

    public IInputProvider InputProvider;

    public AnimationComponent animationComponent;
    private InventoryComponent _inventoryComponent;
    private InventorySystem _inventorySystem;
    public InventorySlotsComponent inventorySlotsComponent;
    
    public Controller player => ContextManager.Instance.player;
    private Action<InputContext> BookOpenCloseHandler;
    private bool _isBookOpen = false;

    public InventoryViewComponent InventoryViewComponent = new InventoryViewComponent();
    public PlayerStatsView playerStats = new PlayerStatsView();

    public Action<float> MaxHealthUpdater;
    public Action<float> ProtectionUpdater;
    public bool isInited;

    protected override void Awake()
    {
         base.Awake();
    }


    private void Start()
    {

        string isActive = "";

        SaveManager.Instance.GetModule<GlobalSaves>().onGlobalStateChange += OnGlobalStateChange;
        
        if (SaveManager.Instance.GetModule<GlobalSaves>().TryGetData("InventoryActive", out isActive))
        {
            if (isActive == "0")
                return;
        }
        else
            return;

        Init();
    }

    public void OnGlobalStateChange(string key, string val)
    {
        if(key != "InventoryActive")
            return;
        
        if(!isInited && val == "1")
            Init();
        else if(isInited && val == "0")
        {
            if (_isBookOpen)
            {
                BookOpenCloseHandler?.Invoke(new InputContext());
            }
            
            ReferenceClean();
        }
    }
    public void Init()
    {

        _inventoryComponent = player.GetControllerComponent<InventoryComponent>();
        _inventorySystem = player.GetControllerSystem<InventorySystem>();
        InputProvider = player.GetControllerSystem<IInputProvider>();
        _healthComponent = player.GetControllerComponent<HealthComponent>();
        _protectionComponent = player.GetControllerComponent<ProtectionComponent>();

        AddControllerComponent(_inventoryComponent);
        AddControllerComponent(_healthComponent);
        AddControllerComponent(_protectionComponent);
        AddControllerSystem(_inventorySystem);
        AddControllerSystem(InputProvider);

        _inventorySlotSystem = new InventorySlotsSystem();

        _inventorySlotSystem.Initialize(this);

        MaxHealthUpdater = c => playerStats.health.text = $"{c}";
        ProtectionUpdater = c => playerStats.protecton.text = $"{c}";

        MaxHealthUpdater.Invoke(_healthComponent.maxHealth);
        ProtectionUpdater.Invoke(_protectionComponent.Protection);

        _healthComponent.OnMaxHealthDataChanged += MaxHealthUpdater;
        _protectionComponent.OnProtectionChange += ProtectionUpdater;
        SubInput();
        isInited = true;
    }

    public void SubInput()
    {
        BookOpenCloseHandler = c =>
        {
            _isBookOpen = !_isBookOpen;
            if (_isBookOpen)
            {
                InputProvider.GetState().Move.Update(true, Vector2.zero);
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
        if (!isInited)
            return;
        
        if (BookOpenCloseHandler != null)
        {
            InputProvider.GetState().Book.started -= BookOpenCloseHandler;
        }
        
        RemoveControllerSystem(InputProvider);
        
        if(_healthComponent.OnMaxHealthDataChanged != null) 
            _healthComponent.OnMaxHealthDataChanged -= MaxHealthUpdater;
        if(_protectionComponent.OnProtectionChange != null)
            _protectionComponent.OnProtectionChange -= ProtectionUpdater;
        
        base.ReferenceClean();
    }

    public void SetPage(int i)
    {
        _inventorySlotSystem.SetPage(i);
    }

    public void SetFilter(int filter)
    {
        _inventorySlotSystem.SetFilter(InventoryFilterFactory.Get(filter));
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

[System.Serializable]
public struct PlayerStatsView : IComponent
{
    public TextMeshProUGUI health,protecton;
}
