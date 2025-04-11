using Controllers;
namespace Systems
{
    public class WallEdgeClimbSystem : BaseSystem
    {
        private ColorPositioningComponent ColorPositioningComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            ColorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            owner.OnUpdate += Update;
        }
        public void Update()
        {
        }
    }
    
    [System.Serializable]
    public class WallEdgeClimbComponent : IComponent
    {
        
    }
}
