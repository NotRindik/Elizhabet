using System;
using std;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-100)]
public class SaveCapsule : OptimizedController, IInteractable
{
    public string ID => WorldKeyBuilder.Build(this, "Capsule" + name);
    private bool isOpen = true;

    public Transform setPos;

    private AbstractEntity lastEntity;

    protected override void Awake()
    {
        base.Awake();
    }
    private void Start()
    {
        ContextManager.Instance.RegisterCapsule(this);
        InputManager.inputActions.Player.Any.performed += TriggerExitAnim;
    }

    private void OnDestroy()
    {
        InputManager.inputActions.Player.Any.performed -= TriggerExitAnim;
    }

    public void TriggerExitAnim(InputAction.CallbackContext c)
    {
        if (isOpen == true || lastEntity == null)
            return;

        var animC = GetControllerComponent<AnimationComponent>();
        animC.CrossFade("Open", 0.1f);
    }

    public void ReturControllToPlayer()
    {
        lastEntity.GetComponent<SortingGroup>().sortingOrder = 15;

        var playerMove = InputManager.inputActions.Player.Move.ReadValue<Vector2>();
        var proxyInput = lastEntity.GetControllerSystem<ProxyInputState>();

        proxyInput.GetState().Move.Update(false, Vector2.zero);

        lastEntity.GetControllerSystem<ProxyInputState>()
            .SetProvider(new PlayerSourceInput());

        proxyInput.GetState().Move.Update(true, playerMove);
        lastEntity = null;
        isOpen = true;
    }

    public void Save()
    {
        lastEntity.GetComponent<PlayerSaveLoadManager>().PrepareData(this);
        App.Instance.GetService<ManifestSaver>().PrepareData();
        
        SaveManager.Instance.Save();
    }
    
    public void Interact(AbstractEntity interactor)
    {
        var animC = GetControllerComponent<AnimationComponent>();
        Debug.Log("Interract");
        if (animC.GetProgress(0) < 1f && animC.currentState != "")
            return;

        if (!isOpen)
            return;

        var proxyInput = interactor.GetControllerSystem<ProxyInputState>();

        var moveAI = new AIMoveInput()
        {
            target = setPos,
            stopDistance = 0.1f,
            maxSpeed = 1
        };

        proxyInput.SetProvider(moveAI);
        
        moveAI.OnTargetReached += () => EnterCapsule(interactor, animC, playAnimation: true, save: true);
    }
    
    public void SpawnInsideNoSave(AbstractEntity interactor)
    {
        var animC = GetControllerComponent<AnimationComponent>();
        interactor.transform.position = setPos.position;
        interactor.GetComponent<SortingGroup>().sortingOrder = 5;
        
        animC.Play("Close", 0, 1f);
        
        animC.SetAllFired();
        
        var moveAI = new AIMoveInput
        {
            target = setPos,
            stopDistance = 0.1f,
            maxSpeed = 1f
        };
        
        var proxyInput = interactor.GetControllerSystem<ProxyInputState>();
        proxyInput.SetProvider(moveAI);
        
        lastEntity = interactor;
        isOpen = false;
    }

    private void EnterCapsule(AbstractEntity interactor, AnimationComponent animC, bool playAnimation, bool save)
    {
        if (playAnimation)
            animC.CrossFade("Close", 0.1f);

        interactor.transform.position = setPos.position;
        interactor.GetComponent<SortingGroup>().sortingOrder = 5;

        if (save)
        {
            lastEntity = interactor;
            isOpen = false;
        }
    }
    
    public bool CanInteract(AbstractEntity _) => isActiveAndEnabled;
}
public class BaseAI : IInputProvider
{
    public bool isActive = true;

    protected AbstractEntity owner;
    protected MonoBehaviour mono;
    protected InputState _inputState;

    protected Transform transform;
    protected GameObject gameObject;
    
    public virtual InputState GetState()
    {
        return _inputState;
    }

    public virtual void Initialize(AbstractEntity owner)
    {
        this.owner = owner;
        mono = owner;
        
        transform = mono.transform;
        gameObject = mono.gameObject;
    }
    
    public void SetState(InputState state)
    {
        _inputState = state;
    }
    
    public void Update()
    {
        if (!isActive)
            return;

        OnUpdate();
    }
    public virtual void OnUpdate()
    {
    }
}
public class AIMoveInput : BaseAI, IDisposable
{
    public Transform target;
    public float stopDistance = 0.1f;
    public float maxSpeed = 1f;

    public event Action OnTargetReached;
    public event Action OnEmergencyShutdown;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        owner.OnUpdate += Update;
    }

    public override void OnUpdate()
    {
        if (target == null || _inputState == null)
            return;

        Vector2 direction = target.position - transform.position;
        direction.y = 0;
        float distance = direction.magnitude;

        Vector2 moveInput = Vector2.zero;
        
        var move = InputManager.inputActions.Player.Move.ReadValue<Vector2>();
        
        if (move.x * direction.x < 0)
        {
            var playerMove = InputManager.inputActions.Player.Move.ReadValue<Vector2>();

            _inputState.Move.Update(false, Vector2.zero);
            OnEmergencyShutdown?.Invoke();
            owner.GetControllerSystem<ProxyInputState>()
                .SetProvider(new PlayerSourceInput());

            _inputState.Move.Update(true, playerMove);

            return;
        }


        if (distance > stopDistance)
        {
            float speedFactor = Mathf.Clamp01(distance);
            Vector3 dir = direction.normalized * maxSpeed * speedFactor;
            moveInput = new Vector2(dir.x, dir.z);
            _inputState.Move.Update(true, moveInput);
        }
        else
        {
            _inputState.Move.Update(false, Vector2.zero);
            OnTargetReached?.Invoke();
            
            isActive = false;
        }
    }

    void IDisposable.Dispose()
    {
        OnTargetReached = null;
        OnEmergencyShutdown = null;
        if (owner != null)
            owner.OnUpdate -= Update;
    }
}