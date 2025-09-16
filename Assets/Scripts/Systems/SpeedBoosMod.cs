using Assets.Scripts.Systems;
using Controllers;
using NUnit.Framework.Interfaces;
using UnityEngine;

namespace Systems
{
    public class SpeedBoostMod : BaseModificator
    {
        private SpeedBoostComponent speedBoostComponent;
        private MoveComponent moveComponent;
        private ControllersBaseFields controllers;
        private AnimationComponentsComposer animationComposer;
        private float speedTemp;
        private bool isSetData;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            speedBoostComponent = _modComponent.GetModComponent<SpeedBoostComponent>();
            controllers = owner.GetControllerComponent<ControllersBaseFields>();
            moveComponent = owner.GetControllerComponent<MoveComponent>();
            animationComposer = owner.GetControllerComponent<AnimationComponentsComposer>();
            speedTemp = moveComponent.speedMultiplierDynamic;
            isSetData = false;
            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            bool noMove = Mathf.Abs(controllers.rb.linearVelocityX) < 0.4f || moveComponent.direction == Vector2.zero;

            if (noMove)
            {
                if (!isSetData) // сбросить только один раз
                {
                    speedBoostComponent.speedBoostTime = speedBoostComponent.speedBoostTimeMax;
                    moveComponent.speedMultiplierDynamic = speedTemp;
                    animationComposer.SetSpeedOfParts(1, "Torso", "Legs");
                    isSetData = true;
                }
                return;
            }

            isSetData = false;

            speedBoostComponent.speedBoostTime = Mathf.Max(speedBoostComponent.speedBoostTime - Time.deltaTime, 0);
            float t = 1f - (speedBoostComponent.speedBoostTime / speedBoostComponent.speedBoostTimeMax);

            moveComponent.speedMultiplierDynamic = Mathf.Lerp(
                moveComponent.speedMultiplierDynamic,
                speedTemp * speedBoostComponent.maxSpeed,
                t
            );

            animationComposer.SetSpeedOfParts(1 + t, "Torso", "Legs");
        }

    }

    public struct SpeedBoostComponent : IComponent
    {
        public float maxSpeed,speedBoostTime,speedBoostTimeMax;

        public SpeedBoostComponent(float maxSpeed, float speedBoostTimeMax)
        {
            this.maxSpeed = maxSpeed;
            this.speedBoostTime = speedBoostTimeMax;   
            this.speedBoostTimeMax = speedBoostTimeMax;
        }
    }
}
