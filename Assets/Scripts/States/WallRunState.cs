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
            var wallRunsSys = _entityController.GetControllerSystem<WallRunSystem>();
            if (wallRunsSys.IsActive)
            {
                _entityController.GetControllerComponent<AnimationComponentsComposer>().CrossFadeState("WallRun", 0.1f);
                _entityController.GetControllerSystem<WallRunSystem>().Update();
            }
        }
        public void Update()
        {
        }
        public void Exit()
        {
        }
    }
}