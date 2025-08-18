using Assets.Scripts;
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
        private FSMSystem _fsm;
        

        private SlideComponent _slideComponent;
        public DashState(EntityController entityController)
        {
            this.entityController = entityController;
        }

        public void Enter()
        {
            _slideComponent = entityController.GetControllerComponent<SlideComponent>();
            _frictionSystem = entityController.GetControllerSystem<FrictionSystem>();
            _fsm = entityController.GetControllerSystem<FSMSystem>();
            foreach (var systems in entityController.Systems.Values)
            {
                if ((systems is HookSystem safe))
                {
                    safe.StopCoroutineSafely();
                }
            }
            
            if (_slideComponent.SlideProcess != null)
            {
                _frictionSystem.IsActive = false;
                entityController.GetControllerSystem<SlideDashSystem>().OnDash();
            }
            else
            {
                entityController.GetControllerSystem<DashSystem>().OnDash();
            }

            _moveSystem = entityController.GetControllerSystem<MoveSystem>();
        }
        public void FixedUpdate()
        {
            if (_slideComponent.SlideProcess == null)
            {
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