using Systems;

namespace Controllers
{
    public class TwoHandedMeleeWeapon : OneHandedWeapon
    {
        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            itemPositioningSystem = new TwoHandPositioning();
            itemPositioningSystem.Initialize(this);
        }
        
    }
}