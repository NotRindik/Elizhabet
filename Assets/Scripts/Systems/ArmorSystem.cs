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

            SetArmourByData();

        }

        private void SetArmourByData()
        {
            foreach (ArmourPart part in Enum.GetValues(typeof(ArmourPart)))
            {
                ItemStack itemStack = null;

                // приоритет: Cosmetic > Armour
                if (_armourComponent.HasArmour(ArmourType.Cosmetic, part))
                {
                    itemStack = _armourComponent.GetArmour(ArmourType.Cosmetic, part);
                }
                else if (_armourComponent.HasArmour(ArmourType.Armour, part))
                {
                    itemStack = _armourComponent.GetArmour(ArmourType.Armour, part);
                }

                if (itemStack != null)
                {
                    var armourItem = itemStack.GetItemComponent<ArmourItemComponent>();
                    var tex = armourItem.armourSprite.texture;
                    _armourComponent.armourMaterial.SetTexture(_armourComponent.armourMaterialPair[part], tex);
                }
                else
                {
                    _armourComponent.armourMaterial.SetTexture(_armourComponent.armourMaterialPair[part], null);
                }
            }
        }

        private void OnItemAdd(ArmourType type, ArmourPart part, ItemStack stack)
        {
            // Если добавляем Cosmetic — сразу ставим
            if (type == ArmourType.Cosmetic)
            {
                var texture = stack.GetItemComponent<ArmourItemComponent>().armourSprite.texture;
                _armourComponent.armourMaterial.SetTexture(_armourComponent.armourMaterialPair[part], texture);
                return;
            }

            // Если добавляем Armour — проверяем, есть ли Cosmetic
            if (!_armourComponent.HasArmour(ArmourType.Cosmetic, part))
            {
                var texture = stack.GetItemComponent<ArmourItemComponent>().armourSprite.texture;
                _armourComponent.armourMaterial.SetTexture(_armourComponent.armourMaterialPair[part], texture);
            }
            // Иначе — ничего не делаем, потому что приоритет у Cosmetic
        }


        private void OnItemRemove(ArmourType type, ArmourPart part)
        {
            _armourComponent.armourMaterial.SetTexture(_armourComponent.armourMaterialPair[part], null);
            SetArmourByData();
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

            var armour = itemStack.GetItemComponent<ArmourItemComponent>();
            if (armour != null)
                armour.isEquiped = true;
            OnItemAdd?.Invoke(type,part,itemStack);
        }

        public bool RemoveArmour(ArmourType type, ArmourPart part)
        {
            if (armorData.TryGetValue(type, out var partDict))
            {
                if (partDict.TryGetValue(part, out var itemStack))
                {
                    var armour = itemStack.GetItemComponent<ArmourItemComponent>();
                    if (armour != null)
                        armour.isEquiped = false; 

                    if (partDict.Remove(part))
                    {
                        if (partDict.Count == 0)
                            armorData.Remove(type);

                        OnItemRemove?.Invoke(type, part);
                        return true;
                    }
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
