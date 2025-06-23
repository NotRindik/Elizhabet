using Controllers;
using Systems;

namespace States
{
    public class AttackState:IState
    {
        private Controller _controller;
        private AttackSystem _attackSystem;
        public AttackState(Controller controller)
        {
            _controller = controller;
            _attackSystem = controller.GetControllerSystem<AttackSystem>();
        }

        public void Enter()
        {
            _attackSystem.Update();
        }
        public void Update()
        {
        }
        public void Exit()
        {
        }
    }
}