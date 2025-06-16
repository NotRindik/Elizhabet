using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class GrablingHookState: IState
    {
        private EntityController _entityController;
        private MoveSystem _moveSystem;
        private FrictionSystem _frictionSystem;
        private SlideComponent _slideComponent;
        private FSMSystem _fsm;
        public GrablingHookState(EntityController entityController)
        {
            _entityController = entityController;
            _moveSystem = _entityController.GetControllerSystem<MoveSystem>();
            _frictionSystem = _entityController.GetControllerSystem<FrictionSystem>();
            _slideComponent = _entityController.GetControllerComponent<SlideComponent>();
            _fsm = _entityController.GetControllerSystem<FSMSystem>();
        }

        public void Enter()
        {
            _entityController.GetControllerSystem<HookSystem>().Update();    
            if(_slideComponent.SlideProcess != null)
                _fsm.SetState(new SlideState((PlayerController)_entityController));
        }
        public void Update()
        {
            _moveSystem.Update();
        }
        public void Exit()
        {
        }
    }
}