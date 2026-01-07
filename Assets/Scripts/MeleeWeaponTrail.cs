using Controllers;
using UnityEngine;

public class MeleeWeaponTrail : MonoBehaviour
{
    public MeleeWeapon MeleeWeapon;

    private MeleeComponent meleeComponent;

    private void Start()
    {
        meleeComponent = MeleeWeapon.GetControllerComponent<MeleeComponent>();
    }
}
