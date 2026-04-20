using System;
using UnityEngine;

namespace Systems
{
    public class HeadRotSystem : BaseSystem, IDisposable
    {
        private HeadRotComponent _headRotComponent;
        private IInputProvider _inputProvider;
        private Vector3 _pointScreenPos;
        private Camera _camera => ContextManager.Instance.mainCamera;
        private float angle;
        private float currAngle;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _headRotComponent = owner.GetControllerComponent<HeadRotComponent>();
            _inputProvider = owner.GetControllerSystem<IInputProvider>();

            _inputProvider.GetState().Point.performed += UpdatePointPos;
            owner.OnUpdate += Update;
        }

        public void UpdateHeadRot()
        {
            _pointScreenPos.z = Mathf.Abs(_camera.transform.position.z);
            var worldPos = _camera.ScreenToWorldPoint(_pointScreenPos);

            Transform neckT = _headRotComponent.neckPivot;

            Vector2 dir = worldPos - neckT.position;
            float distance = dir.magnitude;

            if (owner.transform.localScale.x < 0)
                dir.x = -dir.x;

            if (distance >= _headRotComponent.maxLookDistance)
            {
                angle = 0f;
                return;
            }

            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            angle += _headRotComponent.angleOffset;
        }

        public override void OnUpdate()
        {
            UpdateHeadRot();
            base.OnUpdate();

            float target = Mathf.Clamp(
                angle,
                _headRotComponent.angleConstrain.x,
                _headRotComponent.angleConstrain.y
            );

            float speed = (Mathf.Approximately(target, 0f))
                ? _headRotComponent.returnSpeed
                : _headRotComponent.maxDelta;

            currAngle = Mathf.MoveTowardsAngle(
                currAngle,
                target,
                speed * Time.deltaTime
            );

            _headRotComponent.neckPivot.localRotation =
                Quaternion.Euler(0f, 0f, currAngle);
        }


        public void Dispose()
        {
            _inputProvider.GetState().Point.performed -= UpdatePointPos;
            owner.OnUpdate -= Update;
        }

        public void UpdatePointPos(InputContext c)
        {
            _pointScreenPos = c.ReadValue<Vector2>();
        }
        public void UpdatePointPos(Vector2 c)
        {
            _pointScreenPos = c;
        }
    }

    [System.Serializable]
    public struct HeadRotComponent : IComponent
    {
        [Sirenix.OdinInspector.MinMaxSlider(-180f,180f)]
        public Vector2 angleConstrain;

        public float angleOffset,
            maxDelta,
            maxLookDistance,
            returnSpeed;

        public Transform neckPivot;
    }

}