using System;
using UnityEngine;

public abstract class Items : MonoBehaviour
{
    public Action OnTake;
    public Action OnThrow;
    public virtual void TakeUp()
    {
        OnTake?.Invoke();
    }

    public virtual void Throw()
    {
        OnThrow?.Invoke();
    }
}
