using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class SaveListener : SerializedMonoBehaviour
{
    [Serializable]
    public class SaveCondition
    {
        public enum SaveSource { GlobalSaves, WorldObjectsStateSave }
        public enum ConditionType { Exists, Equals, NotEquals, NotExists }

        [HorizontalGroup("row")]
        public SaveSource source = SaveSource.GlobalSaves;

        [HorizontalGroup("row")]
        public ConditionType condition = ConditionType.Exists;

        public string key;

        [ShowIf("NeedsExpectedValue")]
        public string expectedValue;
        
        private bool NeedsExpectedValue() =>
            condition == ConditionType.Equals || condition == ConditionType.NotEquals;

        public bool Evaluate()
        {
            KVPSaves module = source == SaveSource.GlobalSaves
                ? (KVPSaves)SaveManager.Instance.GetModule<GlobalSaves>()
                : SaveManager.Instance.GetModule<WorldObjectsStateSave>();

            bool exists = module.Exist(key);

            string actual = null;
            if (exists && (condition == ConditionType.Equals || condition == ConditionType.NotEquals))
            {
                actual = source == SaveSource.GlobalSaves
                    ? SaveManager.Instance.GetModule<GlobalSaves>().GetData(key)
                    : SaveManager.Instance.GetModule<WorldObjectsStateSave>().GetData(key);
            }

            return condition switch
            {
                ConditionType.Exists    => exists,
                ConditionType.NotExists => !exists,
                ConditionType.Equals    => exists && actual == expectedValue,
                ConditionType.NotEquals => !exists || actual != expectedValue,
                _ => false
            };
        }
    }

    [Serializable]
    public class SaveRule
    {
        public enum LogicMode { All, Any }

        [BoxGroup("Rule")]
        public string label;

        [BoxGroup("Rule")]
        public LogicMode logic = LogicMode.All;

        [BoxGroup("Rule")]
        public SaveCondition[] conditions;

        [BoxGroup("Rule")]
        public BetterEvent onTrue;

        [BoxGroup("Rule"), Space]
        public bool hasOnFalse;

        [BoxGroup("Rule"), ShowIf("hasOnFalse")]
        public BetterEvent onFalse;

        public void Evaluate()
        {
            bool result = logic == LogicMode.All
                ? System.Linq.Enumerable.All(conditions, c => c.Evaluate())
                : System.Linq.Enumerable.Any(conditions, c => c.Evaluate());

            if (result) onTrue.Invoke();
            else if (hasOnFalse) onFalse.Invoke();
        }
    }

    public enum TriggerMoment { OnStart, OnEnable, Manual }

    public TriggerMoment triggerOn = TriggerMoment.OnStart;

    [Space]
    public SaveRule[] rules;

    private void Start()
    {
        if (triggerOn == TriggerMoment.OnStart)
            EvaluateAll();
    }

    private void OnEnable()
    {
        if (triggerOn == TriggerMoment.OnEnable)
            EvaluateAll();
    }

    [Button]
    public void EvaluateAll()
    {
        foreach (var rule in rules)
            rule.Evaluate();
    }

    public void SetGlobal(string keyValue)
    {
        var parts = keyValue.Split('=');
        if (parts.Length == 2)
            SaveManager.Instance.GetModule<GlobalSaves>().SetData(parts[0], parts[1]);
    }

    public void SetWorld(string keyValue)
    {
        var parts = keyValue.Split('=');
        if (parts.Length == 2)
            SaveManager.Instance.GetModule<WorldObjectsStateSave>().SetData(parts[0], parts[1]);
    }

    public void SetWorldFromObject(string localKey)
    {
        var key = WorldKeyBuilder.Build(this, localKey);
        SaveManager.Instance.GetModule<WorldObjectsStateSave>().SetData(key, "true");
    }
}