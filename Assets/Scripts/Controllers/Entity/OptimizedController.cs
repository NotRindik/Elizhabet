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
            Components = new Dictionary<Type, IComponent>(components.Length);
            foreach (var c in components)
            {
                if (c == null) continue;
                Components[c.GetType()] = c;
            }
        }

        if (systems != null && systems.Length > 0)
        {
            Systems = new Dictionary<Type, ISystem>(systems.Length);
            foreach (var s in systems)
            {
                if (s == null) continue;
                Systems[s.GetType()] = s;
            }
        }
    }

    private void InitSystems()
    {
        if (Systems == null) return;

        foreach (var system in Systems.Values)
            system.Initialize(this);
    }

    public override void AddControllerComponent<T>(T component)
    {
        Components ??= new Dictionary<Type, IComponent>(4);
        Components[typeof(T)] = component;
    }

    public override void AddControllerSystem<T>(T system)
    {
        Systems ??= new Dictionary<Type, ISystem>(2);
        Systems[typeof(T)] = system;
        system.Initialize(this);
    }

    public override T GetControllerComponent<T>()
    {
        if (Components == null) return default;
        return Components.TryGetValue(typeof(T), out var c)
            ? (T)c
            : default;
    }

    public override T GetControllerSystem<T>()
    {
        if (Systems == null) return null;

        if (Systems.TryGetValue(typeof(T), out var exact))
            return exact as T;

        foreach (var sys in Systems.Values)
            if (sys is T match)
                return match;

        return null;
    }

    private void OnDestroy()
    {
        if (Systems == null) return;

        foreach (var sys in Systems.Values)
            if (sys is IDisposable d)
                d.Dispose();
    }

    public void Destroy() => Destroy(gameObject);
}
