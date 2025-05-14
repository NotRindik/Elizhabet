using Controllers;
using Systems;

namespace States
{
    public class DashState : IState
    {
        private EntityController entityController;
        private MoveSystem _moveSystem;
        public DashState(EntityController entityController)
        {
            this.entityController = entityController;
        }

        public void Enter()
        {
            entityController.GetControllerSystem<DashSystem>().OnDash();
            _moveSystem = entityController.GetControllerSystem<MoveSystem>();
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