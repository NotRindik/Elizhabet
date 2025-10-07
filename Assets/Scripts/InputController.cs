using System;
using System.Collections.Generic;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Collections.LowLevel.Unsafe;
using System.Drawing;
using Unity.Collections;

public interface IInputProvider:ISystem
{
    public InputState GetState();
}

public class InputState : IComponent
{
    public Dictionary<string, InputState> actionState = new();

    // GamePlay
    public InputActionState Move = new();
    public InputActionState Look = new();
    public InputActionState WeaponWheel = new();
    public InputActionState Attack = new();
    public InputActionState Interact = new();
    public InputActionState Crouch = new();
    public InputActionState Jump = new();
    public InputActionState Previous = new();
    public InputActionState Next = new();
    public InputActionState OnDrop = new();
    public InputActionState Dash = new();
    public InputActionState Slide = new();
    public InputActionState GrablingHook = new();
    public InputActionState Fly = new();
    public InputActionState Point = new();

    // UI
    public InputActionState Book = new();
    public InputActionState Back = new();
    public InputActionState Navigate = new();
    public InputActionState Submit = new();
    public InputActionState Cancel = new();
    public InputActionState FastPress = new();
}

public class PlayerSourceInput : IInputProvider, IDisposable
{
    public Input inputActions;
    public InputState InputState;

    private readonly List<(InputAction action, Action<InputAction.CallbackContext> handler)> _handlers = new();

    private void Bind<T>(InputAction action, InputActionState target) where T : unmanaged
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

        // GamePlay
        Bind<Vector2>(inputActions.Player.Move, InputState.Move);
        Bind<Vector2>(inputActions.Player.Look, InputState.Look);
        Bind<Vector2>(inputActions.Player.WeaponWheel, InputState.WeaponWheel);
        Bind<bool>(inputActions.Player.Attack, InputState.Attack);
        Bind<bool>(inputActions.Player.Interact, InputState.Interact);
        Bind<bool>(inputActions.Player.Crouch, InputState.Crouch);
        Bind<bool>(inputActions.Player.Jump, InputState.Jump);
        Bind<bool>(inputActions.Player.Previous, InputState.Previous);
        Bind<bool>(inputActions.Player.Next, InputState.Next);
        Bind<bool>(inputActions.Player.OnDrop, InputState.OnDrop);
        Bind<bool>(inputActions.Player.Dash, InputState.Dash);
        Bind<bool>(inputActions.Player.Slide, InputState.Slide);
        Bind<bool>(inputActions.Player.GrablingHook, InputState.GrablingHook);
        Bind<Vector2>(inputActions.Player.Point, InputState.Point);

        // UI
        Bind<bool>(inputActions.UI.BookOpen, InputState.Book);
        Bind<Vector2>(inputActions.UI.Navigate, InputState.Navigate);
        Bind<bool>(inputActions.UI.Submit, InputState.Submit);
        Bind<bool>(inputActions.UI.Cancel, InputState.Cancel);
        Bind<bool>(inputActions.UI.FastAction, InputState.FastPress);
        Bind<bool>(inputActions.UI.Back, InputState.Back);
    }

    public void OnUpdate() { }
}



public unsafe struct InputContext
{
    public void* _value;

    public T ReadValue<T>() where T : unmanaged => *(T*)_value;
    public void SetValue<T>(T val) where T : unmanaged => *(T*)_value = val;
}


public unsafe class InputActionState : IDisposable
{
    public event Action<InputContext> started;
    public event Action<InputContext> performed;
    public event Action<InputContext> canceled;

    private bool _isPressed;
    private bool _wasPressed;
    private InputContext _context;

    public bool IsPressed => _isPressed;

    public bool Enabled = true;
    public Type type;

    public T ReadValue<T>() where T : unmanaged => _context.ReadValue<T>();
    public void SetValue<T>(T val) where T : unmanaged => _context.SetValue(val);

    public bool IsValid() => _context._value != null;
    public void Update<T>(bool isPressed, T value) where T : unmanaged
    {
        if(Enabled == false)
            return;
        Init<T>();
        if(type != typeof(T))
        {
            Debug.LogError($"input Type was Changed from {type} to {typeof(T)}");
            return;
        }
            
        _wasPressed = _isPressed;
        _isPressed = isPressed;
        _context.SetValue(value);

        if (!_wasPressed && _isPressed)
            started?.Invoke(_context);

        if (_isPressed)
            performed?.Invoke(_context);

        if (_wasPressed && !_isPressed)
            canceled?.Invoke(_context);
    }

    public void Init<T>() where T :unmanaged
    {
        if(_context._value == null)
        {
            _context = new InputContext();
            int size = sizeof(T);
            int align = UnsafeUtility.AlignOf<T>();
            _context._value = UnsafeUtility.Malloc(size, align, Allocator.Persistent);
            UnsafeUtility.MemClear(_context._value, size); // <--- обязательно!
            type = typeof(T);
        }
    }

    public void Dispose()
    {
        if (_context._value != null)
        {
            UnsafeUtility.Free(_context._value, Unity.Collections.Allocator.Persistent);
            _context._value = null;
        }
    }
}
