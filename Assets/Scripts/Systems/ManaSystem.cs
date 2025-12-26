using Controllers;
using System;
using UnityEngine;
namespace Systems
{

    public class ManaSystem : BaseSystem
    {
        private ManaComponent _manaComponent;
        public void UseM­ana(float takingMana, Action afterManaUse)
        {
            if (CanTakeMana(takingMana))
            {
                Debug.Log("Недостаточно маны");
                return;
            }
            _manaComponent.CurrMana -= takingMana;
            _manaComponent.currTimeToManaStartRecover = _manaComponent.timeToManaStartRecover;
            afterManaUse?.Invoke();
        }

        public bool CanTakeMana(float takingMana)
        {
            return _manaComponent.CurrMana < takingMana;
        }

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _manaComponent = base.owner.GetControllerComponent<ManaComponent>();
            _manaComponent.CurrMana = _manaComponent.MaxMana;

            owner.OnFixedUpdate += Update;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if(_manaComponent.CurrMana < _manaComponent.MaxMana)
            {
                if(_manaComponent.currTimeToManaStartRecover <= 0)
                {
                    _manaComponent.CurrMana = Mathf.MoveTowards(_manaComponent.CurrMana, _manaComponent.MaxMana, _manaComponent.manaRecoverySpeed * Time.deltaTime);
                }
                else
                {
                    _manaComponent.currTimeToManaStartRecover -= Time.deltaTime;
                }
            }
        }
    }

    [System.Serializable]
    public class ManaComponent : IComponent
    {
        [SerializeField] private float _maxMana;
        [SerializeField] private float _currMana;
        [SerializeField] public float timeToManaStartRecover, currTimeToManaStartRecover;
        [SerializeField] public float manaRecoverySpeed;

        public float MaxMana
        {
            get => _maxMana;
            set
            {
                _maxMana = value;
                OnMaxManaDataChanged?.Invoke(_maxMana);
            }
        }

        public float CurrMana
        {
            get => _currMana;
            set
            {
                _currMana = value;
                OnCurrManaDataChanged?.Invoke(_currMana);
            }
        }
        public Action<float> OnCurrManaDataChanged;
        public Action<float> OnMaxManaDataChanged;
    }
}