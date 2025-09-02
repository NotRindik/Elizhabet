using Assets.Scripts;
using System;
using System.Collections.Generic;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class ShootableWeapon : Weapon
    {
        public ShootableComponent shootableComponent = new ShootableComponent();
        public ShootableSystem shootableSystem;
        public HandsRotatoningSystem handsRotatoningSystem;
        public ProjectileComponent projectileComponent;

        private ManaSystem _manaSystem;

        private Action<bool> shootContextHandler;
        private Action<Vector2> handler;

        public override void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(ShootableComponent));
            base.InitAfterSpawnFromInventory(invComponents);

        }

        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            shootContextHandler = a => shootableSystem.Update();
            handler = c => shootableComponent.pointPos = c;

            handsRotatoningSystem = owner.GetControllerSystem<HandsRotatoningSystem>();
            inputComponent.input.GetState().Attack.performed += shootContextHandler;
            animationComponent.animations["RightHand"].animator.enabled = false;
            animationComponent.LockParts("RightHand");
            inputComponent.input.GetState().Point.performed += handler;


            _manaSystem = owner.GetControllerSystem<ManaSystem>();

            AddControllerSystem(_manaSystem);
            shootableSystem = new ShootableSystem();
            shootableSystem.Initialize(this);
        }

        public override void FixedUpdate()
        {
            base.LateUpdate();

            if (itemComponent.currentOwner == null)
                return;

            Vector3 worldPos = Camera.main.ScreenToWorldPoint(shootableComponent.pointPos);
            worldPos.z = 0; // чтобы не уехало по Z

            Vector2 weaponPos = itemComponent.currentOwner.transform.position;
            Vector2 dir = (worldPos - (Vector3)weaponPos).normalized;

            // считаем дистанцию до курсора
            float dist = Vector2.Distance(weaponPos, worldPos);

            // коэффициент отдачи [0..1], где 1 = без отдачи, 0 = сильная отдача
            float recoilFactor = Mathf.Clamp01(1f - shootableComponent.currentRecoil);

            // позиция руки с учётом отдачи
            Vector2 recoilTarget = (weaponPos + dir * dist * recoilFactor);

            handsRotatoningSystem?.RotateHand(Side.Right, recoilTarget);
            shootableComponent.currentRecoil = Mathf.MoveTowards(
    shootableComponent.currentRecoil,
    0f,
    Time.deltaTime * shootableComponent.recoilRecovery
);

        }

        protected override void ReferenceClean()
        {
            if (isSelected)
            {
                if (handler != null)
                {
                    inputComponent.input.GetState().Point.performed -= handler;
                    inputComponent.input.GetState().Attack.performed -= shootContextHandler;
                }
                if (animationComponent != null)
                {
                    animationComponent.UnlockParts("RightHand");
                    animationComponent.animations["RightHand"].animator.enabled = true;
                }
                _manaSystem = null;
                handsRotatoningSystem = null;
            }

            base.ReferenceClean();
        }

    }

    [System.Serializable]
    public class ShootableComponent : IComponent
    {
        public float ManaCost;
        public float Cooldown;
        public float LastShotTime;

        public Vector2 recoilOffset;
        public float currentRecoil;      // текущее значение [0..1]
        public float recoilStrength = 0.3f; // на сколько уменьшается дистанция при выстреле
        public float recoilRecovery = 2f;

        public Vector2 pointPos;

        public Transform firePos,gilsapos;

        public ProjectileController projectilePrefab;
        public ParticleSystem shotFireParticle,gilzaParticle,boomParticle;
    }

    public class ShootableSystem : BaseSystem 
    {
        private ShootableComponent _shootable;
        private WeaponComponent weaponComponent;
        private ProjectileComponent _projectileComponent;
        private ManaSystem _manaSystem;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _shootable = owner.GetControllerComponent<ShootableComponent>();
            weaponComponent = owner.GetControllerComponent<WeaponComponent>();
            _projectileComponent = owner.GetControllerComponent<ProjectileComponent>();
            _manaSystem = owner.GetControllerSystem<ManaSystem>();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            TryShoot();
        }

        private void TryShoot()
        {
            if (Time.time - _shootable.LastShotTime < _shootable.Cooldown)
                return;

            _shootable.LastShotTime = Time.time;
            _manaSystem.UseMana(_shootable.ManaCost,() =>
            {
                _shootable.currentRecoil = Mathf.Min(1f, _shootable.currentRecoil + _shootable.recoilStrength);
                ProjectileController instance = UnityEngine.Object.Instantiate(_shootable.projectilePrefab, _shootable.firePos.position, transform.rotation);
                instance.projectileComponent = _projectileComponent;

                Vector2 dir = (_shootable.firePos.position - _shootable.gilsapos.position).normalized;

                // сохраняем в компонент гравитации
                var gravity = instance.GetControllerComponent<CustomGravityComponent>();
                gravity.gravityVector = dir; // теперь это нормализованный вектор

                var audioInst = AudioManager.instance.PlaySoundEffect($"{FileManager.WeaponsSFX}Guns/Deagle", volume: 0.1f, pitch: UnityEngine.Random.Range(0.8f, 1.2f));
                float projectileSpeed = 10f;
                gravity.gravityVector *= projectileSpeed;

                _shootable.shotFireParticle.Emit(10);
                _shootable.gilzaParticle.Emit(1);
                _shootable.boomParticle.Emit(1);
            });

        }
    }
}