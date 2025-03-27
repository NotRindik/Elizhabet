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

    public Input inputActions = new Input();
}

public class SorceInput : IInputProvider
{
    private InputState state = new InputState();

    public InputState GetState()
    {
        state.inputActions.Player.Move.Enable();
        state.movementDirection = state.inputActions.Player.Move.ReadValue<Vector2>();
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