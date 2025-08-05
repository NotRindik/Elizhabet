using Assets.Scripts;
using Controllers;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Systems
{
    public class BALLSWeapon : TwoHandedMeleeWeapon
    {
        public ParticleSystem[] particle;
        public AnimationComponent weaponAnimationComponent;

        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);

            foreach (var item in particle)
            {
                item.gameObject.SetActive(true);
            }
            weaponAnimationComponent.CrossFade("BALLSActive",0.1f);
            AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}WeaponsSFX/BALLS/ChuchChuch",loop: true, volume: 0.3f);
        }

        public override void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(AnimationComponent));
            base.InitAfterSpawnFromInventory(invComponents);
        }

        public override void Throw()
        {
            base.Throw();

            foreach (var item in particle)
            {
                item.gameObject.SetActive(false);
            }
            weaponAnimationComponent.CrossFade("BALLSIdle", 0.1f);

            AudioManager.instance.StopSoundEffect($"{FileManager.SFX}/WeaponsSFX/BALLS/ChuchChuch");
        }
    }
}