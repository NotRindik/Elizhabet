using System;
using System.Collections.Generic;
using Systems;
using UnityEngine;

public class OptimizedController : AbstractEntity
{
    [SerializeReference, SubclassSelector]
    private IComponent[] components;

    [SerializeReference, SubclassSelector]
    private ISystem[] systems;

    private Dictionary<Type, IComponent> _componentMap;
    private Dictionary<Type, ISystem> _systemMap;


    private void Awake()
    {
        mono = this;
        BuildInfrastructure();
        InitSystems();
    }

    private void Update() => OnUpdate?.Invoke();
    private void FixedUpdate() => OnFixedUpdate?.Invoke();
    private void LateUpdate() => OnLateUpdate?.Invoke();

    private void BuildInfrastructure()
    {
        if (components != null && components.Length > 0)
        {
            _componentMap = new Dictionary<Type, IComponent>(components.Length);
            foreach (var c in components)
            {
                if (c == null) continue;
                _componentMap[c.GetType()] = c;
            }
        }

        if (systems != null && systems.Length > 0)
        {
            _systemMap = new Dictionary<Type, ISystem>(systems.Length);
            foreach (var s in systems)
            {
                if (s == null) continue;
                _systemMap[s.GetType()] = s;
            }
        }
    }

    private void InitSystems()
    {
        if (_systemMap == null) return;

        foreach (var system in _systemMap.Values)
            system.Initialize(this);
    }

    public override void AddControllerComponent<T>(T component)
    {
        _componentMap ??= new Dictionary<Type, IComponent>(4);
        _componentMap[typeof(T)] = component;
    }

    public override void AddControllerSystem<T>(T system)
    {
        _systemMap ??= new Dictionary<Type, ISystem>(2);
        _systemMap[typeof(T)] = system;
        system.Initialize(this);
    }

    public override T GetControllerComponent<T>()
    {
        if (_componentMap == null) return default;
        return _componentMap.TryGetValue(typeof(T), out var c)
            ? (T)c
            : default;
    }

    public override T GetControllerSystem<T>()
    {
        if (_systemMap == null) return null;

        if (_systemMap.TryGetValue(typeof(T), out var exact))
            return exact as T;

        foreach (var sys in _systemMap.Values)
            if (sys is T match)
                return match;

        return null;
    }

    private void OnDestroy()
    {
        if (_systemMap == null) return;

        foreach (var sys in _systemMap.Values)
            if (sys is IDisposable d)
                d.Dispose();
    }
}
