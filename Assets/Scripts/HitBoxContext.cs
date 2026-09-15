using System;
using UnityEngine;

public class HitBoxContext : MonoBehaviour
{
    public AbstractEntity Entity;
    
    public Collider2D[] Hitboxes;

    private void OnValidate()
    {
        Entity ??= GetComponentInParent<AbstractEntity>();
        Hitboxes ??= GetComponents<Collider2D>();
    }
}
