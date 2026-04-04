using Systems;
using UnityEngine;

public class ExtraRespawnWrapper : MonoBehaviour
{
    public DamageComponent dmg;
    public void StartRespawn() => ContextManager.Instance.extraSpawnManager.StartRespawn(dmg);
}
