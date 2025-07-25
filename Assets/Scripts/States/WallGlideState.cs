
using Controllers;
using Systems;


namespace States
{

    internal class WallGlideState : IState
    {
        private EntityController _entityController;
        private AnimationComponent _animationComponent;
        private WallGlideSystem _wallGlideSystem;
        private WallGlideComponent _wallGlideCompnent;
        private MoveSystem _moveSystem;

        public WallGlideState(EntityController entityController)
        {
            _entityController = entityController;
        }

        public void Enter()
        {

            _wallGlideSystem = _entityController.GetControllerSystem<WallGlideSystem>();
            _moveSystem = _entityController.GetControllerSystem<MoveSystem>();

            _animationComponent = _entityController.GetControllerComponent<AnimationComponent>();
            _wallGlideCompnent = _entityController.GetControllerComponent<WallGlideComponent>();

            _animationComponent.CrossFade("WallGlide", 0.1f);
        }
        public void Update()
        {
            if(_animationComponent.currentState != "WallGlide")
            {
                _animationComponent.CrossFade("WallGlide", 0.1f);
            }
            _wallGlideSystem.Update();
            _moveSystem.Update();
        }
        public void Exit()
        {
        }
    }
}