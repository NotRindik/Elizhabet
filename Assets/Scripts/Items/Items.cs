using System;
using Systems;
using UnityEngine;

public abstract class Items : MonoBehaviour
{
    public Action OnTake;
    public Action OnThrow;

    public Rigidbody2D rb;
    public Collider2D col;

    public virtual void TakeUp(Transform)
    {
        OnTake?.Invoke();
        backpackComponent.items[0].transform.position = colorPositioning.vectorValue[0];
        backpackComponent.items[0].rb.bodyType = RigidbodyType2D.Static;
        backpackComponent.items[0].col.isTrigger = true;
        Vector2 perpendicularDirection = new Vector2(-colorPositioning.direction.y, colorPositioning.direction.x);
        Vector2 collinearDirection = -colorPositioning.direction.normalized;
        float angle = Mathf.Atan2(collinearDirection.y, collinearDirection.x) * Mathf.Rad2Deg;
        backpackComponent.items[0].transform.rotation = Quaternion.Euler(0, 0, angle);
        backpackComponent.items[0].transform.localScale = new Vector3(1, owner.transform.localScale.x, 1);
    }

    public virtual void Throw()
    {
        OnThrow?.Invoke();
    }
}
