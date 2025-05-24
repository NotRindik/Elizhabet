using Controllers;
using Systems;

namespace States
{
    public class SlideState : IState
    {
        private PlayerController _playerController;
        public SlideState(PlayerController playerController) => _playerController = playerController;
        public FrictionSystem FrictionSystem;

        public void Enter()
        {
            FrictionSystem = _playerController.GetControllerSystem<FrictionSystem>();
            FrictionSystem.IsActive = false;
            _playerController.GetControllerSystem<SlideSystem>().Update();
        }
        public void Update()
        {
        }
        public void Exit()
        {
            FrictionSystem.IsActive = true;
        }
    }
}