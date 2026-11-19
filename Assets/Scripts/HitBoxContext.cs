using System;
using UnityEngine;

public class HitBoxContext : MonoBehaviour
{
    public AbstractEntity Entity;
    
    public Collider2D[] Hitboxes;

    private void OnValidate()
    {
        if (!Entity) Entity = GetComponentInParent<AbstractEntity>();
        Hitboxes ??= GetComponents<Collider2D>();
    }
}
