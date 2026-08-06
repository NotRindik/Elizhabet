using System;
using Controllers;
using Systems;
public static class EventBus
{
    public static Action<HitInfo> OnDamageApplied;
    public static Action<PlayerController> OnPlayerChange;
}
