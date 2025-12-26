using Controllers;
using DefaultNamespace;
using System;
using UnityEngine;

namespace Systems
{
    public abstract class TakeDamageSystemBase : BaseSystem, IDisposable
    {
        private HealthComponent _healthComponent;

        public void Dispose()
        {
            _healthComponent.OnTakeHit -= OnTakeHit;
        }

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _healthComponent = owner.GetControllerComponent<HealthComponent>();
            _healthComponent.OnTakeHit += OnTakeHit;
        }

        protected void OnTakeHit(HitInfo info)
        {
            if(!IsActive)
                return;
            TakeHit(info);
        }

        protected abstract void TakeHit(HitInfo info);
    }

    public class  PlayerTakeDamageSystem : TakeDamageSystemBase
    {
        private ParticleComponent _pc;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _pc = owner.GetControllerComponent<ParticleComponent>();
        }
        protected override void TakeHit(HitInfo info)
        {
            TimeManager.StartHitStop(0.3f,0.3f,0.4f,mono);
            var pos = info.GetHitPos();
            if(pos != default) 
                _pc.bloodParticlePrefab.transform.position = pos;
            _pc.bloodParticlePrefab.Emit(30);
        }
    }
}