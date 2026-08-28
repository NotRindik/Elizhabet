using Assets.Scripts.Systems;
using Controllers;
using System;

namespace Systems 
{
    public unsafe class FallDamageMod : BaseModificator, IDisposable
    {
        private FallDamageModComponent _fallDamageMod;
        private ControllersBaseFields baseFields;
        private AttackComponent attackComponent;

        public void Dispose()
        {
            ref var fallDmgC = ref _modComponent.GetModBySystem(this).GetComponentByRef<FallDamageModComponent>();
            std.Unsafe.Free(fallDmgC.damagePtr);
            owner.OnUpdate -= Update;
        }

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            ref var fallDmgC = ref _modComponent.GetModBySystem(this).GetComponentByRef<FallDamageModComponent>();
            
            fallDmgC.damagePtr = std.Unsafe.MallocData(fallDmgC.damageAdderConfig);
            
            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            attackComponent = owner.GetControllerComponent<AttackComponent>();

            _fallDamageMod = fallDmgC;

            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (baseFields.rb.linearVelocityY < -0.2f)
            {
                if (!attackComponent.damageModifire.Raw.Contains((IntPtr)_fallDamageMod.damagePtr))
                {
                    attackComponent.damageModifire.Add((IntPtr)_fallDamageMod.damagePtr);
                }
            }
            else
            {
                if (attackComponent.damageModifire.Raw.Contains((IntPtr)_fallDamageMod.damagePtr))
                    attackComponent.damageModifire.Remove((IntPtr)_fallDamageMod.damagePtr);
            }
        }
    }

    public unsafe struct FallDamageModComponent : IComponent
    {
        public DamageComponent* damagePtr;
        public DamageComponent damageAdderConfig;
    }
 }