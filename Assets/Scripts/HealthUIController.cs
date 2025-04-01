using UnityEngine;

namespace Systems
{
    public class HealthUIController: MonoBehaviour
    {
        public HealthUIData healthUIData;
    }

    [System.Serializable]
    public class HealthUIData : IComponent
    {
        public float minHp;
        public float maxHp;
        public float currentHp;
    }
}