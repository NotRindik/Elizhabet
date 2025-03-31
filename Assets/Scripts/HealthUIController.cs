using UnityEngine;

namespace Systems
{
    public class HealthUIController
    {
        public HealthUIData healthUIData;
    }

    [System.Serializable]
    public class HealthUIData : BaseComponent
    {
        public float minHp;
        public float maxHp;
        public float currentHp;
    }
}