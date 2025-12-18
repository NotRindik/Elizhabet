using Assets.Scripts.Systems;
using Controllers;
using System;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Systems 
{
    public unsafe class FallDamageMod : BaseModificator, IDisposable
    {
        private FallDamageModComponent _fallDamageMod;
        private ControllersBaseFields baseFields;
        private AttackComponent attackComponent;

        public void Dispose()
        {
            UnsafeUtility.Free(_fallDamageMod.damageAdder,Unity.Collections.Allocator.Persistent);
        }

        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            _fallDamageMod = _modComponent.GetModComponent<FallDamageModComponent>();
            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            attackComponent = owner.GetControllerComponent<AttackComponent>();

            owner.OnUpdate += Update;
        }

        public unsafe override void OnUpdate()
        {
            base.OnUpdate();

            if (baseFields.rb.linearVelocityY < -0.2f)
            {
                if (!attackComponent.damageModifire.Raw.Contains((IntPtr)_fallDamageMod.damageAdder))
                {
                    attackComponent.damageModifire.Add((IntPtr)_fallDamageMod.damageAdder);
                }
            }
            else
            {
                if (attackComponent.damageModifire.Raw.Contains((IntPtr)_fallDamageMod.damageAdder))
                    attackComponent.damageModifire.Remove((IntPtr)_fallDamageMod.damageAdder);
            }
        }
    }

    public unsafe struct FallDamageModComponent : IComponent
    {
        public DamageComponent* damageAdder;

        public FallDamageModComponent(DamageComponent* damageComponent)
        {
            damageAdder = damageComponent;
        }
    }
 }