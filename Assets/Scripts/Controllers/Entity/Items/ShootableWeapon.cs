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

        private Action<InputContext> shootContextHandler;
        private Action<InputContext> handler;
        private Action<Vector3> SpriteFlipHandler;

        public override void InitAfterSpawnFromInventory(Dictionary<Type, IComponent> invComponents)
        {
            nonInitComponents.Add(typeof(ShootableComponent));
            base.InitAfterSpawnFromInventory(invComponents);

        }

        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            shootContextHandler = a => shootableSystem.Update();
            handler = c => shootableComponent.pointPos = c.ReadValue<Vector2>();

            handsRotatoningSystem = owner.GetControllerSystem<HandsRotatoningSystem>();
            inputComponent.input.GetState().Attack.performed += shootContextHandler;
            if (animationComponent != null)
            {
                animationComponent.animations["RightHand"].animator.enabled = false;
                animationComponent.LockParts("RightHand");
            }
            inputComponent.input.GetState().Point.performed += handler;


            _manaSystem = owner.GetControllerSystem<ManaSystem>();

            AddControllerSystem(_manaSystem);
            shootableSystem = new ShootableSystem();
            shootableSystem.Initialize(this);
            SpriteFlipHandler = c => CalculateHandPos();
            spriteFlipComponent.OnFlip += SpriteFlipHandler;
        }

        public override void FixedUpdate()
        {
            base.LateUpdate();

            if (itemComponent.currentOwner == null)
                return;

            CalculateHandPos();

        }

        private void CalculateHandPos()
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(shootableComponent.pointPos);
            worldPos.z = 0;

            Vector2 weaponPos = itemComponent.currentOwner.transform.position;
            Vector2 dir = (worldPos - (Vector3)weaponPos).normalized;

            float dist = Vector2.Distance(weaponPos, worldPos);

            float recoilFactor = Mathf.Clamp01(1f - shootableComponent.currentRecoil);

            Vector2 recoilTarget = (weaponPos + dir * dist * recoilFactor);

            handsRotatoningSystem?.RotateHand(Side.Right, recoilTarget);
            shootableComponent.currentRecoil = Mathf.MoveTowards(
    shootableComponent.currentRecoil,
    0f,
    Time.deltaTime * shootableComponent.recoilRecovery
);
        }

        public override void Throw()
        {
            OnThrow?.Invoke();
            baseFields.rb.bodyType = RigidbodyType2D.Dynamic;
            baseFields.rb.AddForce((transform.position - itemComponent.currentOwner.transform.position) * 15, ForceMode2D.Impulse);
            foreach (var col in baseFields.collider)
            {
                col.isTrigger = false;
            }
            ReferenceClean();
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
                if (SpriteFlipHandler != null)
                {
                    spriteFlipComponent.OnFlip -= SpriteFlipHandler;
                }   
                if (animationComponent != null)
                {
                    animationComponent.UnlockParts("RightHand");
                    if(animationComponent.animations["RightHand"].animator) 
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

        public EventSoundInstance shootEvent;
    }

    public class ShootableSystem : BaseSystem 
    {
        private ShootableComponent _shootable;
        private WeaponComponent weaponComponent;
        private ProjectileComponent _projectileComponent;
        private ManaSystem _manaSystem;
        private HealthSystem _healthSystem;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _shootable = owner.GetControllerComponent<ShootableComponent>();
            weaponComponent = owner.GetControllerComponent<WeaponComponent>();
            _projectileComponent = owner.GetControllerComponent<ProjectileComponent>();
            _healthSystem = owner.GetControllerSystem<HealthSystem>();
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
                instance.weaponComponent = weaponComponent;
                Vector2 dir = (_shootable.firePos.position - _shootable.gilsapos.position).normalized;

                // сохраняем в компонент гравитации
                var gravity = instance.GetControllerComponent<CustomGravityComponent>();
                gravity.gravityVector = dir; // теперь это нормализованный вектор

                var audioInst = AudioManager.instance.PlayEvent(_shootable.shootEvent);
                float projectileSpeed = 10f;
                gravity.gravityVector *= projectileSpeed;

                _shootable.shotFireParticle.Emit(10);
                _shootable.gilzaParticle.Emit(1);
                _shootable.boomParticle.Emit(1);

                _healthSystem.TakeHit(new HitInfo(1));
            });

        }
    }
}