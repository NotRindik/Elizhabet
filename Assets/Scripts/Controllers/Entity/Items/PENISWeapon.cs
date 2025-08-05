using Assets.Scripts;
using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{

    public class PENISWeapon : OneHandedWeapon
    {
        [SerializeField] public PenisWeaponComponent penisWeaponComponent = new PenisWeaponComponent();
        [SerializeField] public SpriteRenderer spriteRenderer;

        public override void Update()
        {
            base.Update();
            var a = penisWeaponComponent.semenParticle.shape;
            a.rotation = new Vector2(-90 * transform.localScale.y, a.rotation.y);
        }

        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            meleeWeaponSystem = new PenisWeaponAttackSystem();
            meleeWeaponSystem.Initialize(this);
        }

        public override void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(PenisWeaponComponent));
            base.InitAfterSpawnFromInventory(invComponents);
        }


        public override void AttackHandle()
        {
            penisWeaponComponent.combo++;

            int combo = Mathf.Clamp(penisWeaponComponent.combo, 1, 10);

            Color startColor = Color.white;
            Color maxColor = new Color32(255, 120, 120, 255);

            float t = (combo - 1) / 9f;
            spriteRenderer.color = Color.Lerp(startColor, maxColor, t);

            base.AttackHandle();

            if (penisWeaponComponent.combo == 10)
                penisWeaponComponent.combo = 1;
        }
    }

    [System.Serializable]
    public class PenisWeaponComponent : IComponent
    {
        public ParticleSystem semenParticle;
        public int combo;
    }


    public class PenisWeaponAttackSystem : OneHandAttackSystem
    {
        private PenisWeaponComponent _penisWeaponComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _penisWeaponComponent = owner.GetControllerComponent<PenisWeaponComponent>();
        }

        protected override IEnumerator AttackProcess()
        {
            owner.StartCoroutine(CummingProcess());
            return base.AttackProcess();
        }

        public IEnumerator CummingProcess()
        {
            yield return new WaitForSeconds(0.08f);
            _penisWeaponComponent.semenParticle.Emit(_penisWeaponComponent.combo);
        }

    }
}