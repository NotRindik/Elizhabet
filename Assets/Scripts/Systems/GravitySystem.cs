using Controllers;
using UnityEngine;

namespace Systems
{
    public class GravityScalerSystem : BaseSystem
    {
        private ControllersBaseFields baseFields;
        private GravityScalerComponent gravityScaler;
        private GroundingComponent groundingComponent;
        private JumpComponent jumpComponent;

        private bool isFallingApplied; // флаг, применили ли множитель
        private float gravityScaleTemp = 1;

        public override void Initialize(IController owner)
        {
            base.Initialize(owner);
            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            gravityScaler = owner.GetControllerComponent<GravityScalerComponent>();
            groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            jumpComponent = owner.GetControllerComponent<JumpComponent>();
            gravityScaleTemp = baseFields.rb.gravityScale;
        }

        public unsafe override void OnUpdate()
        {
            base.OnUpdate();
            if(groundingComponent.isGround == true)
            {
                gravityScaler.timer = 0;
            }
            else gravityScaler.timer += Time.deltaTime;
            if (jumpComponent.isJumpCuted)
            {
                baseFields.rb.gravityScale = gravityScaleTemp * gravityScaler.fallGravityMultiplier;
                baseFields.rb.linearVelocity = new Vector2(baseFields.rb.linearVelocityX, Mathf.Max(baseFields.rb.linearVelocityY, -gravityScaler.maxFallSpeed));
            }
            else if(baseFields.rb.linearVelocityY < 0)
            {
                baseFields.rb.gravityScale = gravityScaleTemp * gravityScaler.fallGravityMultiplier;
                baseFields.rb.linearVelocity = new Vector2(baseFields.rb.linearVelocityX, Mathf.Max(baseFields.rb.linearVelocityY, -gravityScaler.maxFallSpeed));
            }
            else if (Mathf.Abs(baseFields.rb.linearVelocityY) < gravityScaler.jumpHangTimeThreshold)
            {
                baseFields.rb.gravityScale = gravityScaleTemp * gravityScaler.jumpHangGravityMult;
            }
            else
            {
                baseFields.rb.gravityScale = gravityScaleTemp;
            }
        }
    }

    [System.Serializable]
    public class GravityScalerComponent : IComponent
    {
        public float fallGravityMultiplier,maxFallSpeed, jumpHangTimeThreshold, jumpHangGravityMult,timer;
    }
}
