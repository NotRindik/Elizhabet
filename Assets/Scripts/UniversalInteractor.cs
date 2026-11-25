using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Systems;
using UnityEngine;
using Object = UnityEngine.Object;

public class UniversalInteractor : SerializedMonoBehaviour,IInteractable
{
    public Interaction[] interactions = {};
    public InteractionCondition[] Conditions = {new ActiveGameObject()};

    private void Start()
    {
        foreach (var interaction in interactions)
        {
            interaction.Init(this);
        }

        if (IsInteractionSaved())
        {
            foreach (var interaction in interactions)
                interaction.RestoreSaved();
        }

        ApplySavedStateEvents(Conditions);
        foreach (var interaction in interactions)
            ApplySavedStateEvents(interaction.Conditions);
    }

    private bool IsInteractionSaved()
    {
        foreach (var interaction in interactions)
            if (interaction is SaveInteractionState)
                return SaveManager.Instance.GetModule<WorldScope>().HasFlag(this, SaveInteractionState.LocalKey);
        return false;
    }

    private void ApplySavedStateEvents(InteractionCondition[] conditions)
    {
        if (conditions == null) return;
        foreach (var condition in conditions)
            if (condition is SaveStateExistOrTrue saved)
                saved.Apply(this);
    }

    public void Interact(AbstractEntity interactor)
    {
        foreach (var interaction in interactions)
            interaction.Interact(interactor);
    }
    
    public bool CanInteract(AbstractEntity entity)
    {
        foreach (var condition in Conditions)
            if (!condition.ConditionMet(this, null, entity))
                return false;

        foreach (var interaction in interactions)
            if (!interaction.CanExecute(entity))
                return false;

        return true;
    }

}
[System.Serializable]
public class AddItems : Interaction,IProvidesItems
{
    public Item[] items = {};
    
    public IEnumerable<Item> GetItems() => items;
    
    public override void Interact(AbstractEntity interactor)
    {
        var inv = interactor.GetControllerSystem<InventorySystem>();

        foreach (var item in items)
        {
            var inst = Object.Instantiate(item,interactor.transform.position,Quaternion.identity);
            inv.SetItem(inst);
        }
    }
}
[System.Serializable]
public class SetActive : Interaction
{
    public ActiveState[] states = {};
    
    public override void Interact(AbstractEntity interactor)
    {
        foreach (var state in states)
        {
            state.gameObject.SetActive(state.hasActive);
        }
    }

    public override void RestoreSaved()
    {
        foreach (var state in states)
        {
            if (state.gameObject != null)
                state.gameObject.SetActive(state.hasActive);
        }
    }
    
    [System.Serializable]
    public class ActiveState
    {
        public GameObject gameObject;
        public bool hasActive;
    }
}

[System.Serializable]
public class MakeUnUsable : Interaction
{
    public int useCounter = 1;
    public override void Interact(AbstractEntity interactor)
    {
        if(useCounter <= 0)
            return;

        interactionObject.enabled = false;
        
        useCounter--;
    }

    public override void RestoreSaved()
    {
        interactionObject.enabled = false;
        useCounter = 0;
    }
}
[System.Serializable]
public class EnableInventory : Interaction
{
    public override void Interact(AbstractEntity interactor)
    {
        interactor.GetComponent<PlayerManipulator>().InventoryEnabled(true);
    }
}

[System.Serializable]
public class SaveInteractionState : Interaction
{
    public const string LocalKey = "SaveInteractionState";
    public string key => WorldKeyBuilder.Build(interactionObject, LocalKey);

    public override void Interact(AbstractEntity interactor)
    {
        SaveManager.Instance.GetModule<WorldScope>().SetFlag(interactionObject, LocalKey);
        SaveService.Instance.RequestSave();
    }
}

[System.Serializable]
public class SaveStateExistOrTrue : InteractionCondition
{
    public BetterEvent onExist = new BetterEvent(),onNonExist = new BetterEvent();
    public override bool ConditionMet(UniversalInteractor interactionObj,Interaction interaction, AbstractEntity interactor)
    {
        return true;
    }

    public void Apply(UniversalInteractor interactionObj)
    {
        if (SaveManager.Instance.GetModule<WorldScope>().HasFlag(interactionObj, SaveInteractionState.LocalKey))
            onExist.Invoke();
        else
            onNonExist.Invoke();
    }
}

[System.Serializable]
public class ShowTip : Interaction
{
    public TipData tip;
    public override void Interact(AbstractEntity interactor)
    {
        ScreenTipManager.Instance.Show(tip);
    }
}


[System.Serializable]
public abstract class Interaction
{
    protected UniversalInteractor interactionObject;
    public InteractionCondition[] Conditions = {};
    public virtual void Init(UniversalInteractor owner)
    {
        interactionObject = owner;
    }
    
    public bool CanExecute(AbstractEntity entity)
    {
        if(Conditions == null || Conditions.Length == 0) return true;
        
        foreach (var c in Conditions)
            if (!c.ConditionMet(interactionObject, this, entity))
                return false;
        return true;
    }
    
    public abstract void Interact(AbstractEntity interactor);

    public virtual void RestoreSaved()
    {
    }
}

[System.Serializable]
public abstract class InteractionCondition
{
    public abstract bool ConditionMet(UniversalInteractor owner, Interaction interaction, AbstractEntity entity);
}

[System.Serializable]
public class ActiveGameObject : InteractionCondition
{

    public override bool ConditionMet(UniversalInteractor interactionObj,Interaction interaction, AbstractEntity interactor)
    {
        return interactionObj.isActiveAndEnabled;
    }
}
[System.Serializable]
public class InventoryHasFreeSpace : InteractionCondition
{
    public override bool ConditionMet(UniversalInteractor owner, Interaction interaction, AbstractEntity entity)
    {
        var inv = entity.GetControllerSystem<InventorySystem>();
        if (inv == null) return false;

        if (interaction is not IProvidesItems provider)
            return true;

        foreach (var item in provider.GetItems())
            if (!inv.CanAcceptItem(item))
                return false;

        return true;
    }
}

public interface IProvidesItems
{
    IEnumerable<Item> GetItems();
}