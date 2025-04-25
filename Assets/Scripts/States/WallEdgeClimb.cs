using Controllers;
using Systems;

namespace States
{
    public class WallEdgeClimb : IState
    {
        private EntityController _controller;

        private WallEdgeClimbSystem _wallEdgeClimbSystem;
        
        public WallEdgeClimb(EntityController controller)
        {
            _controller = controller;
        }

        public void Enter()
        {
            _wallEdgeClimbSystem = _controller.GetControllerSystem<WallEdgeClimbSystem>();
        }
        public void Update()
        {
            _wallEdgeClimbSystem.Update();
        }
        public void Exit()
        {
            _controller.baseFields.rb.gravityScale = 1;
        }
    }
}