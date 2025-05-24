using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class DashState : IState
    {
        private EntityController entityController;
        private MoveSystem _moveSystem;
        private FrictionSystem _frictionSystem;

        private SlideComponent _slideComponent;
        public DashState(EntityController entityController)
        {
            this.entityController = entityController;
        }

        public void Enter()
        {
            _slideComponent = entityController.GetControllerComponent<SlideComponent>();
            _frictionSystem = entityController.GetControllerSystem<FrictionSystem>();
            if (_slideComponent.SlideProcess != null)
            {
                _frictionSystem.IsActive = false;
                entityController.GetControllerSystem<SlideDashSystem>().OnDash();
            }
            else
                entityController.GetControllerSystem<DashSystem>().OnDash();
            
            _moveSystem = entityController.GetControllerSystem<MoveSystem>();
        }
        public void Update()
        {
            if (_slideComponent.SlideProcess == null)
            {
                Debug.Log("Move");
                _moveSystem.Update();
            }
        }
        public void Exit()
        {
            if (_frictionSystem.IsActive == false)
            {
                _frictionSystem.IsActive = true;
            }
        }
    }
}