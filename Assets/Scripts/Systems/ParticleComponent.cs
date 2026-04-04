using UnityEngine;

namespace Systems
{
    [System.Serializable]
    public struct ParticleComponent: IComponent
    {
        public ParticleSystem groundedParticle;
        public ParticleSystem hitParticlePrefab;
        public ParticleSystem bloodParticlePrefab;
    }
}