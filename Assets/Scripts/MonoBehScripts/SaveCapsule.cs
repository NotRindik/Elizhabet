using System;
using System.Collections;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class SaveCapsule : OptimizedController, IInteractable
{
    private bool isOpen = true;

    public Transform setPos;

    private AbstractEntity lastEntity;

    private void Start()
    {
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
        print("GAME WAS SAVED");
        ManifestSaver.Instance.Save();
    }
    public void Interact(AbstractEntity interactor)
    {
        var animC = GetControllerComponent<AnimationComponent>();
        Debug.Log("Interract");
        if (animC.GetProgress(0) < 1f && animC.currentState != "")
            return;

        var proxyInput = interactor.GetControllerSystem<ProxyInputState>();

        if (isOpen)
        {
            var moveAI = new AIMoveInput()
            {
                target = setPos,
                stopDistance = 0.1f,
                maxSpeed = 1f
            };

            proxyInput.SetProvider(moveAI);
            
            moveAI.OnTargetReached += () =>
            {
                animC.CrossFade("Close", 0.1f);
                interactor.transform.position = setPos.position;
                interactor.GetComponent<SortingGroup>().sortingOrder = 10;
                lastEntity = interactor;
                isOpen = false;
            };
        }
    }
}
public class BaseAI : IInputProvider
{
    public bool isActive = true;

    protected AbstractEntity owner;
    protected MonoBehaviour mono;
    protected InputState _inputState;

    protected Transform transform => mono.transform;
    protected GameObject gameObject => mono.gameObject;
    
    public virtual InputState GetState()
    {
        return _inputState;
    }

    public virtual void Initialize(AbstractEntity owner)
    {
        this.owner = owner;
        mono = (MonoBehaviour)owner;
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

        if (InputManager.inputActions.Player.Move.ReadValue<Vector2>() != Vector2.zero)
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