using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class MeleeImpactSystem : BaseSystem, System.IDisposable
    {
        private MeleeComponent _meleeComponent;
        private HealthComponent HealthComponent => owner.GetControllerComponent<HealthComponent>();
        private ItemComponent ItemComponent => owner.GetControllerComponent<ItemComponent>();
        private AttackComponent _attackComponent;
        private Item _item;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _item = (Item)owner;
            _meleeComponent = owner.GetControllerComponent<MeleeComponent>();

            _meleeComponent.OnFirstHit.AddListener(OnFirstHit);
            _item.OnTake += HandleEquip;
        }

        private void HandleEquip(AbstractEntity playerOwner)
        {
            _attackComponent = playerOwner.GetControllerComponent<AttackComponent>();
        }

        private void OnFirstHit( HitInfo hit)
        {
            if (hit.Target.ExistSys<HealthSystem>() && hit.Target.GetControllerComponent<HealthComponent>().currHealth > 0)
                HealthComponent.currHealth--;
            SelfKnockBack(hit);

            if (HealthComponent.currHealth <= 0)
                _item.DestroyItem();
        }

        private void SelfKnockBack(in HitInfo hit)
        {
            if (_item.itemComponent.currentOwner == null)
                return;

            var selfRb = hit.Attacker.GetControllerComponent<ControllersBaseFields>().rb;

            Vector2 dir = ((Vector2)hit.Target.mono.transform.position - (Vector2)hit.Attacker.transform.position).normalized;

            float similarity = Vector2.Dot(dir, Vector2.down);
            var grounding = _item.itemComponent._currentOwner.GetControllerComponent<GroundingComponent>();
            bool isGrounded = grounding.IsReallyGrounded;

            Debug.Log("POGO was BEFORE: " + _attackComponent.IsPogo);
            
            _attackComponent.IsPogo = _attackComponent.IsPogo ? !isGrounded : !isGrounded && similarity > 0.5f;
            
            Debug.Log($"IS Pogo: {_attackComponent.IsPogo}");

            if (_attackComponent.IsPogo)
            {
                TimeManager.StartHitStop(0.02f, 0.1f);

                float gravity = Mathf.Abs(Physics2D.gravity.y * selfRb.gravityScale);
                float targetHeightAboveEnemy = MeleeComponent.PogoHeight;

                float enemyY = hit.Target.mono.transform.position.y;
                float playerY = hit.Attacker.transform.position.y;

                float heightToReach = (enemyY + targetHeightAboveEnemy) - playerY;

                float requiredVelocity = heightToReach > 0
                    ? Mathf.Sqrt(2f * gravity * heightToReach)
                    : Mathf.Sqrt(2f * gravity * targetHeightAboveEnemy);

                selfRb.linearVelocityY = 0;
                selfRb.linearVelocityY = requiredVelocity;
            }
            else
            {
                float knockbackAngle = 45f;

                Vector2 knockbackDir = -dir;

                float angleSign = -Mathf.Sign(knockbackDir.x);
                knockbackDir = Quaternion.Euler(0f, 0f, knockbackAngle * angleSign) * knockbackDir;

                selfRb.linearVelocity = knockbackDir.normalized * _meleeComponent.pushbackForce;
            }
        }

        public void Dispose()
        {
            _meleeComponent.OnFirstHit.RemoveListener(OnFirstHit);
        }
    }

    public class MeleeEffectSystem : BaseSystem,IDisposable
    {
        private MeleeEffectComponent  _meleeEffectComponent;
        private MeleeComponent  _meleeComponent;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _meleeEffectComponent = owner.GetControllerComponent<MeleeEffectComponent>();
            _meleeComponent = owner.GetControllerComponent<MeleeComponent>();
            _meleeComponent.OnFirstHit.AddListener(OnFirstHit);
        }
        
        private void OnFirstHit(HitInfo hit)
        {
            var inst = new EventSoundInstance(_meleeEffectComponent.hitSound);
            var enemy = hit.Target;
            
            inst.SetData(new MaterialData()
            {
                interaction = "hit",
                material = enemy.GetComponent<AudioMaterialSetter>()?.AudioMaterial
            });
            
            AudioManager.instance.PlayEvent(inst);
            
            TimeManager.StartHitStop(_meleeEffectComponent.duration, _meleeEffectComponent.slowdownFactor);
            
            PlayerCamShake.Instance.Shake(_meleeEffectComponent.shake,1,0);
        }
        public void Dispose()
        {
            _meleeComponent.OnFirstHit.RemoveListener(OnFirstHit);
        }
    }

    [System.Serializable]
    public class MeleeEffectComponent : IComponent
    {
        public EventSound hitSound;
    
        public float duration = 0.01f, slowdownFactor = 0.2f;
        
        public ShakeData shake = new ShakeData(){amplitude = 1,frequency = 1};
        public float shakeDuration = 1;
    }
}