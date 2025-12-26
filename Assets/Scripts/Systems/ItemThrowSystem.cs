
using Controllers;
using NUnit.Framework;
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace Systems
{
    internal class ItemThrowSystem : BaseSystem, IDisposable
    {

        private Coroutine chargingProcess;
        private ItemThrowComponent throwComponent;
        private AnimationComponentsComposer composer;
        private HandsRotatoningSystem handsRotatoningSystem;
        private HandsRotatoningComponent handsRotatoningComponent;
        private InventorySystem inventorySystem;
        private IInputProvider inputProvider;
        private Action<InputContext> pointHandler;
        private Vector2 pointPos;
        private float time;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            throwComponent = owner.GetControllerComponent<ItemThrowComponent>();
            handsRotatoningComponent = owner.GetControllerComponent<HandsRotatoningComponent>();
            composer = owner.GetControllerComponent<AnimationComponentsComposer>();
            handsRotatoningSystem = owner.GetControllerSystem<HandsRotatoningSystem>();
            inventorySystem = owner.GetControllerSystem<InventorySystem>();
            inputProvider = owner.GetControllerSystem<IInputProvider>();
            pointHandler = c => pointPos = c.ReadValue<Vector2>();
            owner.OnGizmosUpdate += OnDrawGizmos;
            inputProvider.GetState().Point.performed += pointHandler;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (chargingProcess == null)
            {
                chargingProcess = mono.StartCoroutine(ChargingProcess());
            }
        }

        public void Throw()
        {
            mono.StartCoroutine(ThrowProcess());
        }

        public IEnumerator ThrowProcess()
        {
            mono.StopCoroutine(chargingProcess);

            float power = throwComponent.timeToMax - time;
            time = throwComponent.throwTime;
            startPos = handsRotatoningComponent.handRotatoning[Side.Right].transform.position;
            bool thrown = false;
            while (time > 0)
            {
                time -= Time.deltaTime;
                var t = time / (throwComponent.throwTime);
                CalculateThrowHandPos(t);

                if (!thrown && t <= 0.7f)
                {
                    thrown = true;
                    Vector3 worldPos = Camera.main.ScreenToWorldPoint(pointPos);
                    Vector2 origin = handsRotatoningComponent.handRotatoning[Side.Right].transform.position;
                    Vector2 toTarget = (Vector2)worldPos - origin;
                    inventorySystem.ThrowItem(toTarget, power * throwComponent.power,throwComponent.torque);
                }
                yield return null;
            }

            yield return null;
            chargingProcess = null;
            composer.animations["RightHand"].animator.enabled = true;
            composer.UnlockParts("RightHand");
        }

        public IEnumerator ChargingProcess()
        {
            throwComponent.isCharging = true;
            time = throwComponent.timeToMax;
            composer.animations["RightHand"].animator.enabled = false;
            composer.LockParts("RightHand");
            startPos = handsRotatoningComponent.handRotatoning[Side.Right].transform.position;

            while (throwComponent.isCharging)
            {
                time -= Time.fixedDeltaTime;
                var t = time/ throwComponent.timeToMax;
                CalculateChargeHandPos(t);
                yield return new WaitForFixedUpdate();
            }

            yield return null;

            composer.animations["RightHand"].animator.enabled = true;
            composer.UnlockParts("RightHand");
            chargingProcess = null;
        }
        Vector2 HandPos;
        Vector2 startPos;
        private void CalculateChargeHandPos(float t)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(pointPos);
            worldPos.z = 0;

            Vector2 toCursor = (worldPos - transform.position).normalized;
            Vector2 opposite = -toCursor;
            Vector2 targetPos = (Vector2)transform.position + opposite + throwComponent.offset;

            HandPos = Vector2.Lerp(targetPos, startPos, t);

            handsRotatoningSystem?.RotateHand(Side.Right, HandPos);
        }
        private void CalculateThrowHandPos(float t)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(pointPos);
            worldPos.z = 0;

            Vector2 toCursor = (worldPos - transform.position).normalized;
            Vector2 targetPos = (Vector2)transform.position + toCursor + throwComponent.offset;

            HandPos = Vector2.Lerp(targetPos, startPos, t);

            handsRotatoningSystem?.RotateHand(Side.Right, HandPos);
        }


        public void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(HandPos, 4/32f);
        }
        public void Dispose()
        {
            inputProvider.GetState().Point.performed += pointHandler;
            owner.OnGizmosUpdate -= OnDrawGizmos;
        }
    }


    [System.Serializable]
    public class ItemThrowComponent : IComponent
    {
        public float timeToMax,power,torque,throwTime;
        public bool isCharging;
        public Vector2 offset = new Vector2(0.1f, 0.7f);
    }
}
