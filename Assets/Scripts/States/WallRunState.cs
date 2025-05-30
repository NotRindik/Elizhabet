using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class WallRunState: IState
    {
        private EntityController _entityController;

        public WallRunState(EntityController entityController)
        {
            _entityController = entityController;
        }

        public void Enter()
        {
            _entityController.GetControllerComponent<AnimationComponent>().CrossFade("VerticalWallRun",0.1f);
            _entityController.GetControllerSystem<WallRunSystem>().OnUpdate();
        }
        public void Update()
        {
        }
        public void Exit()
        {
        }
    }
}