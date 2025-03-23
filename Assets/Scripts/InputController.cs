using System;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IInputProvider
{
    public InputState GetState();
}

public class InputState:IComponent
{
    public Vector2 movementDirection;

    public Action<InputAction.CallbackContext> OnJumpUp = contect => { };
    public Action<InputAction.CallbackContext> OnJumpDown = contect => { };
    public Action<InputAction.CallbackContext> OnInteract = contect => { };
    public Action<InputAction.CallbackContext> OnInteractCancled = contect => { };
    public Action<InputAction.CallbackContext> OnDrop = contect => { };
    public Action<InputAction.CallbackContext> OnNext = contect => { };
    public Action<InputAction.CallbackContext> OnPrev = contect => { };
    public Action<InputAction.CallbackContext> OnAttackPressed = contect => { };
}

public class SorceInput : IInputProvider
{
    private Input inputActions = new Input();
    private InputState state = new InputState();

    public InputState GetState()
    {
        inputActions.Player.Move.Enable();
        inputActions.Player.Jump.Enable();
        inputActions.Player.Interact.Enable();
        inputActions.Player.OnDrop.Enable();
        inputActions.Player.Next.Enable();
        inputActions.Player.Previous.Enable();
        inputActions.Player.Attack.Enable();

        inputActions.Player.Jump.started += state.OnJumpUp; 
        inputActions.Player.Jump.canceled += state.OnJumpDown;
        inputActions.Player.Interact.started += state.OnInteract;
        inputActions.Player.Interact.canceled += state.OnInteractCancled;
        inputActions.Player.OnDrop.canceled += state.OnDrop;

        inputActions.Player.Next.performed += state.OnNext;
        inputActions.Player.Previous.performed += state.OnPrev;
        
        inputActions.Player.Attack.performed += state.OnAttackPressed;

        state.movementDirection = inputActions.Player.Move.ReadValue<Vector2>();
        return state;
    }
}

public class DialogueInput : IInputProvider
{
    public event Action OnJump;
    private InputState state;
    private IInputProvider inputSystem = new SorceInput();

    public InputState GetState()
    {
        state = inputSystem.GetState();
        return state;
    }
}

public class NavigationSystem : IInputProvider
{
    public event Action OnJump;
    private InputState state;
    IInputProvider dialogueInput = new DialogueInput();
    public InputState GetState()
    {
        state = dialogueInput.GetState();
        return state;
    }
}