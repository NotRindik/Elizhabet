using DG.Tweening;
using Systems;

namespace States
{
    [System.Serializable]
    public struct PivotsComponent : IComponent
    {
        public UnityEngine.Transform mainPivot;
    }

    public class PogoState : BasicState
    {
        private Tween _tween;
        private MoveSystem _moveSystem;
        private SpriteFlipSystem _spriteFlipSys;
        private AttackComponent _attackComponent;
        private PivotsComponent _pivotsC;

        public PogoState(AbstractEntity entity) : base(entity)
        {
            _pivotsC = entity.GetControllerComponent<PivotsComponent>();
            _moveSystem = entity.GetControllerSystem<MoveSystem>();
            _spriteFlipSys = entity.GetControllerSystem<SpriteFlipSystem>();
            _attackComponent = entity.GetControllerComponent<AttackComponent>();
        }

        public override void Enter()
        {
            var rot = _pivotsC.mainPivot.transform.rotation;
            _tween = _pivotsC.mainPivot.transform.DORotate(new UnityEngine.Vector3(rot.x,rot.y, 360f), 0.3f,RotateMode.FastBeyond360);
        }
        public override void Update()
        {
            _moveSystem.Update();
            _spriteFlipSys.Update();
        }

        public override void Exit()
        {
            _tween?.Kill();
            var rot = _pivotsC.mainPivot.transform.rotation;
            _attackComponent.IsPogo = false;
            _pivotsC.mainPivot.transform.DORotate(new UnityEngine.Vector3(rot.x, rot.y, 0), 0.1f);
        }
    }


    public abstract class BasicState : IState
    {
        protected AbstractEntity entity;
        public BasicState(AbstractEntity entity)
        {
            this.entity = entity;
        }

        public virtual void Update()
        {

        }

        public abstract void Enter();

        public abstract void Exit();
    }
}
