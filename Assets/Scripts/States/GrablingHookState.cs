using Controllers;

namespace States
{
    public class GrablingHookState: IState
    {
        private EntityController _entityController;
        public GrablingHookState(EntityController entityController)
        {
            _entityController = entityController;
        }

        public void Enter()
        {
            _entityController.GetControllerSystem<HookSystem>().Update();
        }
        public void Update()
        {
        }
        public void Exit()
        {
        }
    }
}