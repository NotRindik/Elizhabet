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

        // Раньше атака держала части через LockParts, и TakeHit сбрасывал это через
        // UnlockAll(). Приоритет решает конфликт сам (Reaction должен стоять выше
        // Action/Locomotion в ассете) — но саму претензию Action на части нужно снять
        // явно, иначе после выхода из TakeHit персонаж продолжит показывать
        // недоигранную атаку вместо возврата в Idle/Walk.
        anim.ClearLayer("Action");

        anim.PlayState("Reaction", "TakeHit");
        sfs.IsActive = false;
    }
    
    public override void Exit()
    {
        sfs.IsActive = true;

        // ВАЖНО: раньше "разблокировка" была разовым действием в начале TakeHit,
        // и следующий CrossFadeState/PlayState просто перезаписывал часть без
        // вопросов. Теперь Reaction физически ПОБЕЖДАЕТ по приоритету — если не
        // снять claim здесь, TakeHit-поза останется навсегда, и вообще ни один
        // другой слой больше не сможет получить эти части ни при каких условиях.
        anim.ClearLayer("Reaction");
    }
}
