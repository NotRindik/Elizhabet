using System;
using System.Collections.Generic;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IInputProvider:ISystem
{
    public InputState GetState();
}

public class InputState : IComponent
{
    //GamePlay

    public InputActionState<Vector2> Move = new InputActionState<Vector2>();
    public InputActionState<Vector2> Look = new InputActionState<Vector2>();
    public InputActionState<Vector2> WeaponWheel = new InputActionState<Vector2>();
    public InputActionState<bool> Attack = new InputActionState<bool>();
    public InputActionState<bool> Interact = new InputActionState<bool>();
    public InputActionState<bool> Crouch = new InputActionState<bool>();
    public InputActionState<bool> Jump = new InputActionState<bool>();
    public InputActionState<bool> Previous = new InputActionState<bool>();
    public InputActionState<bool> Next = new InputActionState<bool>();
    public InputActionState<bool> OnDrop = new InputActionState<bool>();
    public InputActionState<bool> Dash = new InputActionState<bool>(); 
    public InputActionState<bool> Slide = new InputActionState<bool>();
    public InputActionState<bool> GrablingHook = new InputActionState<bool>();
    public InputActionState<bool> Fly = new InputActionState<bool>();
    public InputActionState<Vector2> Point = new InputActionState<Vector2>();
    
    //UI
    public InputActionState<bool> Book = new InputActionState<bool>();
    public InputActionState<bool> Back = new InputActionState<bool>();
    public InputActionState<Vector2> Navigate = new InputActionState<Vector2>();
    public InputActionState<bool> Submit = new InputActionState<bool>();
    public InputActionState<bool> Cancel = new InputActionState<bool>();
    public InputActionState<bool> FastPress = new InputActionState<bool>();
}

public class PlayerSourceInput : IInputProvider, IDisposable
{
    public Input inputActions;
    public InputState InputState;
    
    private readonly List<(InputAction action, Action<InputAction.CallbackContext> handler)> _handlers = new();
    private void Bind<T>(InputAction action, InputActionState<T> target) where T : struct
    {
        Action<InputAction.CallbackContext> handler = ctx =>
        {
            bool isActive = ctx.phase != InputActionPhase.Canceled;
            T value = typeof(T) == typeof(bool)
                ? (T)(object)(ctx.ReadValue<float>() > 0.5f)
                : ctx.ReadValue<T>();
            target.Update(isActive, value);
        };

        action.started += handler;
        action.performed += handler;
        action.canceled += handler;

        _handlers.Add((action, handler));
    }
    public InputState GetState() => InputState;

    public void Dispose()
    {
        foreach (var (action, handler) in _handlers)
        {
            action.started -= handler;
            action.performed -= handler;
            action.canceled -= handler;
        }

        _handlers.Clear();
    }
    public void Initialize(Controller owner)
    {
        inputActions = new Input();
        InputState = new InputState();
        inputActions.Enable();

        //GamePlay
        Bind(inputActions.Player.Move, InputState.Move);
        Bind(inputActions.Player.Look, InputState.Look);
        Bind(inputActions.Player.WeaponWheel, InputState.WeaponWheel);
        Bind(inputActions.Player.Attack, InputState.Attack);
        Bind(inputActions.Player.Interact, InputState.Interact);
        Bind(inputActions.Player.Crouch, InputState.Crouch);
        Bind(inputActions.Player.Jump, InputState.Jump);
        Bind(inputActions.Player.Previous, InputState.Previous);
        Bind(inputActions.Player.Next, InputState.Next);
        Bind(inputActions.Player.OnDrop, InputState.OnDrop);
        Bind(inputActions.Player.Dash, InputState.Dash);
        Bind(inputActions.Player.Slide, InputState.Slide);
        Bind(inputActions.Player.GrablingHook, InputState.GrablingHook);
        Bind(inputActions.Player.Point, InputState.Point);
        
        //UI
        Bind(inputActions.UI.BookOpen, InputState.Book);
        Bind(inputActions.UI.Navigate, InputState.Navigate);
        Bind(inputActions.UI.Submit, InputState.Submit);
        Bind(inputActions.UI.Cancel, InputState.Cancel);
        Bind(inputActions.UI.FastAction, InputState.FastPress);
        Bind(inputActions.UI.Back, InputState.Back);
    }
    public void OnUpdate()
    { }
}

public class InputActionState<T> 
{
    public event Action<T> started;
    public event Action<T> performed;
    public event Action<T> canceled;

    private bool _isPressed;
    private bool _wasPressed;
    private T _value;

    public bool IsPressed => _isPressed;

    public T ReadValue() => _value;

    public bool Enabled = true;
    
    public void Update(bool isPressed, T value)
    {
        if(Enabled == false)
            return;
        _wasPressed = _isPressed;
        _isPressed = isPressed;
        _value = (T)value;

        if (!_wasPressed && _isPressed)
            started?.Invoke(_value);

        if (_isPressed)
            performed?.Invoke(_value);

        if (_wasPressed && !_isPressed)
            canceled?.Invoke(_value);
    }

    public void TriggerStart(object value)
    {
        if(Enabled == false)
            return;
        _value = (T)value;
        started?.Invoke(_value);
    }

    public void TriggerPerform(object value)
    {
        if(Enabled == false)
            return;
        _value = (T)value;
        performed?.Invoke(_value);
    }

    public void TriggerCancel(object value)
    {
        if(Enabled == false)
            return;
        _value = (T)value;
        canceled?.Invoke(_value);
    }
}
