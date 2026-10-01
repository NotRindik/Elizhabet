using Sirenix.OdinInspector;
using Systems;
using UnityEngine;

public class UniversalInteractor : SerializedMonoBehaviour,IInteractable
{
    public Interaction[] interactions = {};
    public InteractionCondition[] Conditions = {new ActiveGameObject()};
    
    public void Interact(AbstractEntity interactor)
    {
        foreach (var interaction in interactions)
        {
            interaction.Interact(interactor);
        }
    }
    public bool CanInteract(AbstractEntity entity)
    {
        foreach (InteractionCondition condition in Conditions)
        {
            if(condition.ConditionMet(this,entity) == false)
                return false;
        }
        return true;
    }
}
[System.Serializable]
public class AddItems : Interaction
{
    public Item[] items = {};
    
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
    
    [System.Serializable]
    public class ActiveState
    {
        public GameObject gameObject;
        public bool hasActive;
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
    public abstract void Interact(AbstractEntity interactor);
}
[System.Serializable]
public abstract class InteractionCondition
{
    public abstract bool ConditionMet(UniversalInteractor interactionObj,AbstractEntity entity);
}

[System.Serializable]
public class ActiveGameObject : InteractionCondition
{

    public override bool ConditionMet(UniversalInteractor interactionObj, AbstractEntity interactor)
    {
        return interactionObj.isActiveAndEnabled;
    }
}

[System.Serializable]
public class InventoryFreeSlots : InteractionCondition
{

    public override bool ConditionMet(UniversalInteractor interactionObj, AbstractEntity entity)
    {
        var inv = entity.GetControllerComponent<InventoryComponent>();
        return true; //TODO позже сделаю
    }
}