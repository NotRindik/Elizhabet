using System.Collections.Generic;
using UnityEngine;

public class WormFolower : MonoBehaviour
{
    public Transform head;
    public List<Transform> segments;

    [Header("Spring Settings")]
    public float segmentLength = 0.5f;
    public float stiffness = 15f;   // сила пружины
    public float damping = 8f;      // затухание
    public int iterations = 2;      // стабильность

    private Vector3[] pos;
    private Vector3[] vel;

    void Start()
    {
        int count = segments.Count;

        pos = new Vector3[count];
        vel = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            segments[i].SetParent(null);
            pos[i] = segments[i].position;
            vel[i] = Vector3.zero;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        Simulate(dt);
        ApplyToTransforms();
    }

    void Simulate(float dt)
    {
        // 1. интеграция (velocity)
        for (int i = 0; i < pos.Length; i++)
        {
            vel[i] *= Mathf.Exp(-damping * dt);
            pos[i] += vel[i] * dt;
        }

        // 2. constraints (несколько итераций)
        for (int k = 0; k < iterations; k++)
        {
            Vector3 prev = head.position;

            for (int i = 0; i < pos.Length; i++)
            {
                Vector3 current = pos[i];

                Vector3 delta = current - prev;
                float dist = delta.magnitude;

                if (dist == 0f) continue;

                float diff = (dist - segmentLength) / dist;

                Vector3 force = delta * diff * stiffness * dt;

                pos[i] -= force;
                vel[i] -= force / dt;

                prev = pos[i];
            }
        }
    }

    void ApplyToTransforms()
    {
        Vector3 prev = head.position;

        for (int i = 0; i < pos.Length; i++)
        {
            segments[i].position = pos[i];

            Vector2 dir = pos[i] - prev;

            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                segments[i].rotation = Quaternion.Euler(0, 0, angle);
            }

            prev = pos[i];
        }
    }
}