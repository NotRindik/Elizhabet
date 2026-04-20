using System;
using UnityEngine;

public class CollisionEvent : MonoBehaviour
{
    public BetterEvent onColEnterAdv;
    public BetterEvent onColExitAdv;
    public BetterEvent onColStayAdv;

    public LayerMask collideLayer;

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (!std.UnityUtilities.Utilities.IsInLayerMask(collideLayer,other.gameObject)) return;

        onColEnterAdv.Invoke();
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        if (!std.UnityUtilities.Utilities.IsInLayerMask(collideLayer,other.gameObject)) return;

        onColExitAdv.Invoke();
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        if (!std.UnityUtilities.Utilities.IsInLayerMask(collideLayer,other.gameObject)) return;

        onColStayAdv.Invoke();
    }
}