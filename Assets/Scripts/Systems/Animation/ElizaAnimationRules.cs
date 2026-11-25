using Controllers;
using States;

namespace Systems
{
    public static class ElizaAnimationRules
    {
        public static void Build(PlayerController player)
        {
            var composer = player.GetControllerComponent<AnimationComponentsComposer>();
            var fsm = player.GetControllerComponent<FsmComponent>();
            var wallRun = player.GetControllerComponent<WallRunComponent>();
            var wallRunSystem = player.GetControllerSystem<WallRunSystem>();
            var dash = player.GetControllerComponent<DashComponent>();
            var attack = player.GetControllerComponent<AttackComponent>();

            composer.Rules("Locomotion")
                .Play("WallEdgeClimb", () => fsm.state is WallLeangeClimb)
                .CrossFade("FallDown", 0.2f, () => fsm.state is WallRunState && wallRun.fallAfterRun)
                .CrossFade("WallRun", 0.1f, () => fsm.state is WallRunState && wallRunSystem.IsActive)
                .Hold(() => fsm.state is WallRunState)
                .Play("Somersault", () => fsm.state is PogoState)
                .CrossFade("Slide", 0.1f, () => fsm.state is SlideState)
                .CrossFade("Slide", 0.1f, () => fsm.state is DashState && dash.isDash && dash.isSlideDash)
                .CrossFade("FallUp", 0.1f, () => fsm.state is DashState && dash.isDash)
                .Hold(() => fsm.state is DashState)
                .CrossFade("FallUp", 0.1f, () => fsm.state is FallUpState)
                .CrossFade("FallDown", 0.1f, () => fsm.state is FallState)
                .CrossFade("Walk", 0f, () => fsm.state is WalkState)
                .CrossFade("Idle", 0.1f, () => fsm.state is IdleState);

            composer.Rules("Action")
                .Play(() => attack.actionAnimation, () => attack.actionAnimation != null, fromStart: true)
                .Play("WallGlide", () => composer.HasRequest("WallGlide"))
                .Clear(() => true);

            composer.Rules("Reaction")
                .Play("TakeHit", () => fsm.state is TakeHitState)
                .Clear(() => true);
        }
    }
}
