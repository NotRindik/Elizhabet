using System;
using System.Collections.Generic;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

public interface IInputProvider:ISystem , IDisposable
{
    public InputState GetState();

    void IDisposable.Dispose()
    {
        GetState().Dispose();
    }
}

public class InputState : IComponent, IDisposable
{
    public Dictionary<string, InputActionState> actionState = new();

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

    public InputState()
    {
        // GamePlay
        actionState.Add(nameof(Move), Move);
        actionState.Add(nameof(Look), Look);
        actionState.Add(nameof(WeaponWheel), WeaponWheel);
        actionState.Add(nameof(Attack), Attack);
        actionState.Add(nameof(Interact), Interact);
        actionState.Add(nameof(Crouch), Crouch);
        actionState.Add(nameof(Jump), Jump);
        actionState.Add(nameof(Previous), Previous);
        actionState.Add(nameof(Next), Next);
        actionState.Add(nameof(OnDrop), OnDrop);
        actionState.Add(nameof(Dash), Dash);
        actionState.Add(nameof(Slide), Slide);
        actionState.Add(nameof(GrablingHook), GrablingHook);
        actionState.Add(nameof(Fly), Fly);
        actionState.Add(nameof(Point), Point);

        // UI
        actionState.Add(nameof(Book), Book);
        actionState.Add(nameof(Back), Back);
        actionState.Add(nameof(Navigate), Navigate);
        actionState.Add(nameof(Submit), Submit);
        actionState.Add(nameof(Cancel), Cancel);
        actionState.Add(nameof(FastPress), FastPress);
    }

    public void Dispose()
    {
        foreach (var item in actionState.Values)
        {
            item.Dispose();
        }
    }
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
    public Type type;

    public T ReadValue<T>() where T : unmanaged
    {
        if (_value == null)
            throw new InvalidOperationException("InputContext is not initialized");
        if (type != typeof(T))
            Debug.LogError($"Input Type is WRONG, you try to read {typeof(T)}, but here is {type}");
        return *(T*)_value;
    }
    public void SetValue<T>(T val) where T : unmanaged
    {
        if (type != typeof(T))
            Debug.LogError($"you try to SET {typeof(T)}, but here is {type}. YOU CANNOT CHANGE INPUT CONTEXT TYPE");
        *(T*)_value = val;
    }
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
    public T ReadValue<T>() where T : unmanaged => _context.ReadValue<T>();
    public void SetValue<T>(T val) where T : unmanaged => _context.SetValue(val);

    public bool IsValid() => _context._value != null;
    public void Update<T>(bool isPressed, T value) where T : unmanaged
    {
        if(Enabled == false)
            return;
        Init<T>();
        if(_context.type != typeof(T))
        {
            Debug.LogError($"input Type was Changed from {_context.type} to {typeof(T)}");
            return;
        }
            
        _wasPressed = _isPressed;
        _isPressed = isPressed;
        _context.SetValue(value);

        if (!_wasPressed && _isPressed)
            started?.Invoke(_context);

        if (_isPressed)
            if (!EqualityComparer<T>.Default.Equals(ReadValue<T>(), value))
                performed?.Invoke(_context);

        if (_wasPressed && !_isPressed)
            canceled?.Invoke(_context);
    }

    private void Init<T>() where T :unmanaged
    {
        if(_context._value == null)
        {
            _context = new InputContext();
            int size = sizeof(T);
            int align = UnsafeUtility.AlignOf<T>();
            _context._value = UnsafeUtility.Malloc(size, align, Allocator.Persistent);
            UnsafeUtility.MemClear(_context._value, size);
            _context.type = typeof(T);
        }
    }

    public void ForceInit<T>() where T : unmanaged
    {
        Dispose();
        Init<T>();
    }

    public void Dispose()
    {
        if (_context._value != null)
        {
            UnsafeUtility.Free(_context._value, Unity.Collections.Allocator.Persistent);
            _context._value = null;
        }
    }

    public InputActionState OnStarted(Action<InputContext> callback)
    {
        started += callback;
        return this;
    }
    public InputActionState OnPerformed(Action<InputContext> callback)
    {
        performed += callback;
        return this;
    }
    public InputActionState OnCanceled(Action<InputContext> callback)
    {
        canceled += callback;
        return this;
    }
}
