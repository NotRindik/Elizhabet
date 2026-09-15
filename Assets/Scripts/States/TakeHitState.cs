using States;
using Systems;

public class TakeHitState : BasicState
{
    public AnimationComponentsComposer anim;
    public SpriteFlipSystem sfs;
    public AttackSystem AttkSys;
        
    public TakeHitState(AbstractEntity entity) : base(entity)
    {
        anim = entity.GetControllerComponent<AnimationComponentsComposer>();
        sfs = entity.GetControllerSystem<SpriteFlipSystem>();
        AttkSys = entity.GetControllerSystem<AttackSystem>();
    }
    
    public override void Enter()
    {
        AttkSys.ForceStopAttack();
        
        anim.ClearLayer("Action");

        anim.PlayState("Reaction", "TakeHit");
        sfs.IsActive = false;
    }
    
    public override void Exit()
    {
        sfs.IsActive = true;
        
        anim.ClearLayer("Reaction");
    }
}
