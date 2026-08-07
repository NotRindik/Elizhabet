using Controllers;
using UnityEngine;

namespace Systems
{
    public class MeleeImpactSystem : BaseSystem, System.IDisposable
    {
        private MeleeComponent _meleeComponent;
        private HealthComponent _healthComponent;
        private AttackComponent _attackComponent;
        private Item _item;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _item = (Item)owner;
            _meleeComponent = owner.GetControllerComponent<MeleeComponent>();
            _healthComponent = owner.GetControllerComponent<HealthComponent>();

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
                _healthComponent.currHealth--;

            SelfKnockBack(hit);

            if (_healthComponent.currHealth <= 0)
                _item.DestroyItem();
        }

        private void SelfKnockBack(in HitInfo hit)
        {
            if (_item.itemComponent.currentOwner == null)
                return;

            var selfRb = hit.Attacker.GetControllerComponent<ControllersBaseFields>().rb;

            Vector2 dir = ((Vector2)hit.Target.mono.transform.position -
                           (Vector2)hit.Attacker.transform.position).normalized;

            float similarity = Vector2.Dot(dir, Vector2.down);

            bool isPlayerInAir = Mathf.Abs(selfRb.linearVelocityY) > 0.3f;
            bool isTargetBelow = hit.Target.mono.transform.position.y < hit.Attacker.transform.position.y - 0.1f;

            _attackComponent.IsPogo = similarity > 0.6f && isPlayerInAir && isTargetBelow;

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
                selfRb.linearVelocityY = 0;
                selfRb.AddForce(_meleeComponent.pushbackForce * 0.25f * Vector2.up, ForceMode2D.Impulse);
            }
        }

        public void Dispose()
        {
            _meleeComponent.OnFirstHit.RemoveListener(OnFirstHit);
        }
    }
}