using Assets.Scripts.Systems;
using Controllers;
using UnityEngine;

namespace Systems 
{
    public class FallDamageMod : BaseModificator
    {
        private FallDamageModComponent _fallDamageMod;
        private ControllersBaseFields baseFields;
        private AttackComponent attackComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _fallDamageMod = _modComponent.GetModComponent<FallDamageModComponent>();
            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            attackComponent = owner.GetControllerComponent<AttackComponent>();

            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (baseFields.rb.linearVelocityY < -0.2f)
            {
                if (!attackComponent.damageModifire.Raw.Contains(_fallDamageMod.damageAdder))
                {
                    Debug.Log("ADED");
                    attackComponent.damageModifire.Add(_fallDamageMod.damageAdder);
                }
            }
            else
            {
                if (attackComponent.damageModifire.Raw.Contains(_fallDamageMod.damageAdder))
                    attackComponent.damageModifire.Remove(_fallDamageMod.damageAdder);
            }
        }
    }

    public struct FallDamageModComponent : IComponent
    {
        public DamageComponent damageAdder;

        public FallDamageModComponent(DamageComponent damageComponent)
        {
            damageAdder = damageComponent;
        }
    }
 }