using UnityEngine;

namespace Systems
{
    [System.Serializable]
    public struct TrailComponent : IComponent
    {
        public TrailRenderer trailRenderer;
    }
}