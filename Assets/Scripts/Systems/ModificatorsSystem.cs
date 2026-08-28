using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Assets.Scripts.Systems;
using Sirenix.Serialization;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.Systems
{
    public class ModificatorsSystem : BaseSystem, IDisposable
    {
        private ModificatorsComponent modificatorsComponent;
        private InventoryComponent inventoryComponent;
        private AbstractEntity owner;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            this.owner = owner;
            modificatorsComponent = owner.GetControllerComponent<ModificatorsComponent>();
            inventoryComponent = owner.GetControllerComponent<InventoryComponent>();

            foreach (var stack in inventoryComponent.armor.Raw.Concat(inventoryComponent.accessories.Raw))
                Equip(stack);
            
            inventoryComponent.accessories.OnItemSet += HandleSlotChanged;
        }
        
        
        private void HandleSlotChanged(ItemStack oldStack, ItemStack newStack)
        {
            if (oldStack != null)
                Unequip(oldStack);

            if (newStack != null)
                Equip(newStack);
        }

        private void Equip(ItemStack stack)
        {
            var modItem = stack?.GetItemComponent<ModificatorItemComponent>();
            if (modItem == null) return;

            var descriptor = modItem.ModDescriptor;
            modificatorsComponent.AddMod(stack, descriptor);
            descriptor.modSys.Initialize(owner);
        }

        private void Unequip(ItemStack stack)
        {
            var modItem = stack?.GetItemComponent<ModificatorItemComponent>();
            if (modItem == null) return;

            modificatorsComponent.RemoveMod(stack);
        }

        public void Dispose()
        {
            inventoryComponent.accessories.OnItemSet -= HandleSlotChanged;
        }
    }

    public class ModificatorsComponent : IComponent
    {
        private readonly Dictionary<ItemStack, ModDescriptor> Descriptors = new();

        public void AddMod(ItemStack stack, ModDescriptor descriptor) => Descriptors[stack] = descriptor;

        public bool TryGetMod(ItemStack stack, out ModDescriptor descriptor) => Descriptors.TryGetValue(stack, out descriptor);

        public bool RemoveMod(ItemStack stack)
        {
            if (Descriptors[stack].modSys is IDisposable d) d.Dispose();
            
            if (!Descriptors.Remove(stack)) return false;
            return true;
        }

        public ModDescriptor GetModBySystem(ISystem system)
        {
            return Descriptors.Values.FirstOrDefault(d => d.modSys == system);
        }
        
        public IEnumerable<ModDescriptor> GetModsOfType(Type type) => Descriptors.Values.Where(d => d.ModificatorType == type);

        public IEnumerable<ModDescriptor> All => Descriptors.Values;

        public void DisposeAll()
        {
            foreach (var d in Descriptors.Values)
                if (d.modSys is IDisposable disposable) disposable.Dispose();
            Descriptors.Clear();
        }
    }
}

[System.Serializable]
public class BaseModificator : BaseSystem
{
    protected ModificatorsComponent _modComponent;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        _modComponent = owner.GetControllerComponent<ModificatorsComponent>();
    }
}

[System.Serializable]
public class ModDescriptor
{
    public Type ModificatorType => modSys.GetType();
    [SerializeReference, SubclassSelector]  public IComponent modComponent;
    [SerializeReference, SubclassSelector]  public BaseModificator modSys;

    public ref T GetComponentByRef<T>() where T : struct, IComponent
    {
        if (modComponent is not T)
            throw new InvalidCastException(
                $"Component is {modComponent.GetType()}, requested {typeof(T)}");

        return ref Unsafe.Unbox<T>(modComponent);
    }
    
    public T GetComponent<T>() where T : class, IComponent
    {
        return (T)modComponent;
    }
}

public class LuckyModificator : ISystem
{

    public void Initialize(AbstractEntity owner)
    {
        throw new NotImplementedException();
    }
    public void OnUpdate()
    {
        throw new NotImplementedException();
    }
}


[Serializable]
public class ModificatorItemComponent : IComponent,ISaveSerialize
{
    public ModificationBodyParts  modificationBodyParts;
    public ModDescriptor ModDescriptor;
}