using Controllers;
using System;
using System.Collections.Generic;
using Systems;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Assets.Scripts.Systems
{
    public class ModificatorsSystem : BaseSystem, IDisposable
    {
        private ModificatorsComponent modificatorsComponent;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            modificatorsComponent = owner.GetControllerComponent<ModificatorsComponent>();

            AddToListAll();

            InitAllSystems(owner);

            SetActiveAllSystem(false);
        }

        private void InitAllSystems(Controller owner)
        {
            foreach (var sys in modificatorsComponent.Systems.Values)
            {
                sys.Initialize(owner);
            }
        }

        public unsafe void AddToListAll()
        {
            LayerMask mask = (1 << LayerMask.NameToLayer("Ground"));

            DamageComponent* fallDamagePtr = (DamageComponent*)UnsafeUtility.Malloc(sizeof(DamageComponent),4,Unity.Collections.Allocator.Persistent);
            DamageComponent* berserkerDamagePtr = (DamageComponent*)UnsafeUtility.Malloc(sizeof(DamageComponent),4,Unity.Collections.Allocator.Persistent);

            *fallDamagePtr = new DamageComponent(1.5f, 1, 1, 1, ElementType.None);
            *berserkerDamagePtr = new DamageComponent(1.5f, 1, 1, 1, ElementType.None);

            AddModComponents(new WallGlideComponent(0.2f, mask),
                new FallDamageModComponent(fallDamagePtr),
                new BerserkerModificatorComponent(berserkerDamagePtr),
                new SpeedBoostComponent(1.5f,3f),
                new SpikeModComponent());

            AddModSystems(new WallGlideSystem(),
                new FallDamageMod(),
                new BerserkerModificator(),
                new LuckyModificator(),
                new SpeedBoostMod(),
                new SpikeModSystem());
        }

        public void SetActiveAllSystem(bool active)
        {
            foreach (var item in modificatorsComponent.Systems.Values)
            {
                item.IsActive = active;
            }
        }

        public void AddModSystems(params BaseSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                modificatorsComponent.AddModSystem(systems[i]);
            }
        }

        public void AddModComponents(params IComponent[] component)
        {
            for (int i = 0; i < component.Length; i++)
            {
                modificatorsComponent.AddModComponent(component[i]);
            }
        }

        public void Dispose()
        {
            foreach (var item in modificatorsComponent.Systems.Values)
            {
                if(item is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }

    public class ModificatorsComponent : IComponent
    {
        public Dictionary<Type, BaseSystem> Systems = new Dictionary<Type, BaseSystem>();
        public Dictionary<Type, IComponent> Components = new Dictionary<Type, IComponent>();

        public Action<ISystem> OnSystemAdd, OnSystemRemoved;

        public void AddModComponent<T>(T component) where T : IComponent
        {
            Components[component.GetType()] = component;
        }

        public T GetModComponent<T>() where T : IComponent
        {
            return Components.ContainsKey(typeof(T)) ? (T)Components[typeof(T)] : default;
        }

        public void AddModSystem<T>(T system) where T : BaseSystem
        {
            Systems[system.GetType()] = system;
            OnSystemAdd?.Invoke(system);
        }

        public void RemoveModSystem<T>(T system) where T : ISystem
        {
            Systems.Remove(system.GetType());
            OnSystemRemoved?.Invoke(system);
        }
        public T GetModSystem<T>() where T : class, ISystem
        {
            if (Systems.TryGetValue(typeof(T), out var exactMatch))
                return exactMatch as T;

            foreach (var system in Systems.Values)
            {
                if (system is T match)
                    return match;
            }

            return null;
        }
    }

    public class BaseModificator : BaseSystem
    {
        protected ModificatorsComponent _modComponent;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _modComponent = owner.GetControllerComponent<ModificatorsComponent>();
        }
    }


    //Для удачи
    public class LuckyModificator : BaseModificator
    {

    }

}
