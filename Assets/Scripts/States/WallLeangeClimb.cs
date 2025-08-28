using Controllers;
using Systems;

namespace States
{
    public class WallLeangeClimb : IState
    {
        private EntityController _controller;

        private LedgeClimbSystem _ledgeClimbSystem;
        
        public WallLeangeClimb(EntityController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _ledgeClimbSystem = _controller.GetControllerSystem<LedgeClimbSystem>();
              _ledgeClimbSystem.Update();
        }
        public void Update()
        {
            _ledgeClimbSystem.Update();
        }
        public void Exit()
        {
            _controller.baseFields.rb.gravityScale = 1;
        }
    }
}