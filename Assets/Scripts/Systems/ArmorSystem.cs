using AYellowpaper.SerializedCollections;
using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public class ArmorSystem : BaseSystem, IDisposable
    {
        private ArmourComponent _armourComponent;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _armourComponent = owner.GetControllerComponent<ArmourComponent>();
            _armourComponent.OnItemAdd += OnItemAdd;
            _armourComponent.OnItemRemove += OnItemRemove;
        }

        private void OnItemAdd(ArmourType type, ArmourPart part, ItemStack stack) 
        {
            _armourComponent.armourMaterial.SetTexture(_armourComponent.armourMaterialPair[part], stack.GetItemComponent<ArmourItemComponent>().armourSprite.texture);
        }

        private void OnItemRemove(ArmourType type, ArmourPart part)
        {
            _armourComponent.armourMaterial.SetTexture(_armourComponent.armourMaterialPair[part], null);
        }

        public void Dispose()
        {
            _armourComponent.OnItemAdd -= OnItemAdd;
            _armourComponent.OnItemRemove -= OnItemRemove;
        }

    }

    [System.Serializable]
    public class ArmourComponent : IComponent
    {

        public Material armourMaterial;
        public SerializedDictionary<ArmourType, SerializedDictionary<ArmourPart, ItemStack>> armorData = new();

        public Dictionary<ArmourPart, string> armourMaterialPair = new()
        {
            {ArmourPart.Head , "_LUT1" },
            {ArmourPart.Torso , "_LUT2" },
            {ArmourPart.Leg , "_LUT3" }
        };

        public Action<ArmourType,ArmourPart,ItemStack> OnItemAdd;
        public Action<ArmourType,ArmourPart> OnItemRemove;

        // Добавить или заменить предмет
        public void AddArmour(ArmourType type, ArmourPart part, ItemStack itemStack)
        {
            if (!armorData.TryGetValue(type, out var partDict))
            {
                partDict = new SerializedDictionary<ArmourPart, ItemStack>();
                armorData[type] = partDict;
            }

            partDict[part] = itemStack;
            OnItemAdd?.Invoke(type,part,itemStack);
        }

        // Удалить предмет
        public bool RemoveArmour(ArmourType type, ArmourPart part)
        {
            if (armorData.TryGetValue(type, out var partDict))
            {
                if (partDict.Remove(part))
                {
                    // Если вложенный словарь стал пустым — можно и его удалить
                    if (partDict.Count == 0)
                        armorData.Remove(type);
                    OnItemRemove.Invoke(type, part);
                    return true;
                }
            }
            return false;
        }

        // Получить предмет
        public ItemStack GetArmour(ArmourType type, ArmourPart part)
        {
            if (armorData.TryGetValue(type, out var partDict))
            {
                if (partDict.TryGetValue(part, out var item))
                    return item;
            }
            return null; // или `default(ItemStack)` если он структура
        }

        // Проверить наличие
        public bool HasArmour(ArmourType type, ArmourPart part)
        {
            return armorData.TryGetValue(type, out var partDict) && partDict.ContainsKey(part);
        }
    }


    public enum ArmourPart
    {
        Head,
        Torso,
        Leg
    }

    public enum ArmourType
    {
        Cosmetic,
        Armour
    }
}
