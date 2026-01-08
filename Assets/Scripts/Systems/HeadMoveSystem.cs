using System;
using UnityEngine;

namespace Systems
{
    public class HeadRotSystem : BaseSystem, IDisposable
    {
        private HeadRotComponent _headRotComponent;
        private IInputProvider _inputProvider;
        private Vector3 _pointScreenPos;
        private Camera _camera;
        private float angle;
        private float currAngle;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _headRotComponent = owner.GetControllerComponent<HeadRotComponent>();
            _inputProvider = owner.GetControllerSystem<IInputProvider>();

            _inputProvider.GetState().Point.performed += UpdatePointPos;
            owner.OnUpdate += Update;
            _camera = Camera.main;
        }

        public void UpdateHeadRot()
        {
            _pointScreenPos.z = Mathf.Abs(_camera.transform.position.z);
            var worldPos = _camera.ScreenToWorldPoint(_pointScreenPos);

            Transform neckT = _headRotComponent.neckPivot;

            Vector2 dir = worldPos - neckT.position;

            if (owner.transform.localScale.x < 0)
                dir.x = -dir.x;

            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            angle += _headRotComponent.angleOffset;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            float target = Mathf.Clamp(
                angle,
                _headRotComponent.angleConstrain.x,
                _headRotComponent.angleConstrain.y
            );

            currAngle = Mathf.MoveTowardsAngle(
                currAngle,
                target,
                _headRotComponent.maxDelta * Time.deltaTime
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
            UpdateHeadRot();
        }
        public void UpdatePointPos(Vector2 c)
        {
            _pointScreenPos = c;
            UpdateHeadRot();
        }
    }

    [System.Serializable]
    public struct HeadRotComponent : IComponent
    {
        [Sirenix.OdinInspector.MinMaxSlider(-180f,180f)]
        public Vector2 angleConstrain;

        public float angleOffset,maxDelta;

        public Transform neckPivot;
    }

}