using Assets.Scripts;
using Controllers;
using Systems;
using UnityEngine;

namespace States
{
    public class SlideState : IState
    {
        private PlayerController _playerController;
        public SlideState(PlayerController playerController) => _playerController = playerController;
        public FrictionSystem FrictionSystem;
        private AudioSource slideSound;
        private DashComponent _dashComponent;
        public void Enter()
        {
            FrictionSystem = _playerController.GetControllerSystem<FrictionSystem>();
            _dashComponent = _playerController.GetControllerComponent<DashComponent>();
            FrictionSystem.IsActive = false;
            _playerController.GetControllerSystem<SlideSystem>().Update();
            if(!slideSound)
                slideSound = AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Slide",loop:true);
        }
        public void Update()
        {
            if(slideSound != null){
                float velocityX = Mathf.Abs(_playerController.baseFields.rb.linearVelocity.x);
                slideSound.pitch = Mathf.Lerp(0, 1, velocityX);
            }
        }
        public void Exit()
        {
            if(slideSound)
                Object.Destroy(slideSound.gameObject);
            
            FrictionSystem.IsActive = true;
        }
    }
}