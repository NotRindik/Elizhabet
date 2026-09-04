using Systems;
using UnityEngine;

public class StomachSawRobotBrain : BaseAI
{
    private FSMSystem _fsmSystem;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());

        _fsmSystem = owner.GetControllerSystem<FSMSystem>();


        var idle = new WanderingIdle(owner);
        _fsmSystem.AddAnyTransition(idle, () => true);
    }
}

public class WanderingIdle : BaseState
{
    private SimpleMoveComponent  _moveComponent;
    private AnimationComponent  _animationComponent;
    private SpriteFlipSystem spriteFlipSystem;

    private float thinkTime;
    private float MaxThinkTime = 2;
    
    public WanderingIdle(AbstractEntity owner) : base(owner)
    {
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
    }
    public override void Enter()
    {
        _animationComponent.Play("Idle");
    }
    public override void Exit()
    {
    }

    public override void Update()
    {
        thinkTime -= Time.deltaTime;

        if (thinkTime <= 0)
        {
            thinkTime = MaxThinkTime;
            
            var moveDir = Random.Range(-1,2);

            _moveComponent.direction.x = moveDir;
            
            spriteFlipSystem.SetFacing(moveDir);
        }
    }
}
