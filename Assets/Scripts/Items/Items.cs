using Controllers;
using System;
using Systems;
using UnityEngine;

public abstract class Items : MonoBehaviour
{
    public Action OnTake;
    public Action OnThrow;

    public Rigidbody2D rb;
    public Collider2D col;

    Controller owner;
    ColorPositioningComponent colorPositioning;

    [SerializeReference] // ѕозвол€ет сериализовать наследников ItemComponent
    protected ItemComponent itemComponent = new ItemComponent();

    public virtual ItemComponent ItemComponent => itemComponent;
    public virtual void TakeUp(ColorPositioningComponent colorPositioning, Controller owner)
    {
        OnTake?.Invoke();
        this.colorPositioning = colorPositioning; 
        this.owner = owner;
        rb.bodyType = RigidbodyType2D.Static;
        col.enabled = false;
    }

    public virtual void Throw()
    {
        OnThrow?.Invoke();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.AddForce((transform.position - owner.transform.position) * 10);
        this.colorPositioning = null;
        this.owner = null;
    }

    public virtual void UpdatePos()
    {
        if (colorPositioning == null)
            return;
        transform.position = colorPositioning.points[0].position;
        
        Vector2 perpendicularDirection = new Vector2(-colorPositioning.direction.y, colorPositioning.direction.x);
        Vector2 collinearDirection = -colorPositioning.direction.normalized;
        float angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        transform.localScale = new Vector3(1, owner.transform.localScale.x, 1);
    }
}

public class ItemComponent : IComponent
{
    public TakeType takeType;
}

public enum TakeType
{
    None,
    ParallelToHand,
    PerpendicularToHand
}