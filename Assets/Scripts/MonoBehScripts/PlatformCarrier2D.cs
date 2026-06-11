using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlatformCarrier2D : SerializedMonoBehaviour
{
    public LayerMask allowedMask;

    public readonly HashSet<Transform> tracked =
        new();

    private Vector3 lastPosition;

    private void Awake()
    {
        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        Vector3 delta =
            transform.position -
            lastPosition;

        if (delta.sqrMagnitude > 0f)
        {
            foreach (Transform target in tracked)
            {
                if (target == null)
                    continue;
                
                target.position +=
                    delta;
            }
        }

        tracked.RemoveWhere(
            x => x == null);

        lastPosition =
            transform.position;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 <<
              collision.gameObject.layer) &
             allowedMask) == 0)
            return;

        tracked.Add(
            collision.transform);
    }

    private void OnCollisionExit2D(
        Collision2D collision)
    {
        tracked.Remove(
            collision.transform);
    }
}