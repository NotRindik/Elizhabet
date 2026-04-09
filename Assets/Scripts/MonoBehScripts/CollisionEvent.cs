using System;
using UnityEngine;

public class CollisionEvent : MonoBehaviour
{
    public BetterEvent onColEnterAdv;
    public BetterEvent onColExitAdv;
    public BetterEvent onColStayAdv;


    private void OnCollisionEnter2D(Collision2D other)
    {
        onColEnterAdv.Invoke();
    }
    
    private void OnCollisionExit2D(Collision2D other)
    {
        onColExitAdv.Invoke();
    }
    
    private void OnCollisionStay2D(Collision2D other)
    {
        onColStayAdv.Invoke();
    }
}
