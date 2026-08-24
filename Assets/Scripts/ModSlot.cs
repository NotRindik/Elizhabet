using System;
using System.Collections.Generic;
public class ModSlot : SlotBase
{
    public ModificationBodyParts  modificationBodyParts;
    
    public override bool CanAccept(DragableItem item)
    {
        return true; //TODO кароче ты потом сделаешь класс для модов
    }
}

public enum ModificationBodyParts
{
    Brain,
    Arm, 
    KneeCup,
    Leg
}

namespace Systems
{
    [System.Serializable]
    public class ModificatorItemComponent : IComponent
    {
        public ModificationBodyParts  modificationBodyParts;
        public Dictionary<Type, BaseSystem> Systems = new Dictionary<Type, BaseSystem>();
        public Dictionary<Type, IComponent> Components = new Dictionary<Type, IComponent>();
    }   
}