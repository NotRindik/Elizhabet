using UnityEngine;

public class HairChain : MonoBehaviour
{
    public Transform root;
    public HairSpriteBufer[] segments;
    public float segmentLength = 0.1f;
    public Vector2 gravity = new(0, -2f);
    [Sirenix.OdinInspector.MinMaxSlider(-180,180)]
    public Vector2 MinMaxAngle;
    Vector2 prevRoot;
    Vector2[] prev;
    int lookBackCount = 3; // например, 3 предыдущих сегмента

    void Awake()
    {
        prevRoot = root.position;
        prev = new Vector2[segments.Length];
        for (int i = 0; i < segments.Length; i++)
            prev[i] = segments[i].transform.position;
    }


    void LateUpdate()
    {

        Vector2 rootVel = (Vector2)root.position - prevRoot;
        prevRoot = root.position;

        segments[0].transform.position = root.position;
        prev[0] = (Vector2)root.position - rootVel;

        for (int i = 1; i < segments.Length; i++)
        {
            Vector2 cur = segments[i].transform.position;
            Vector2 vel = cur - prev[i];
            prev[i] = cur;

            float d = Mathf.Lerp(0.9f, 0.7f, (float)i / segments.Length);
            cur += gravity * Time.deltaTime * Time.deltaTime;
            segments[i].transform.position = cur;

            // --- усреднённое направление ---
            Vector2 avgDir = Vector2.zero;
            int count = 0;

            // j ограничен так, чтобы i - j - 1 >= 0
            for (int j = 1; j <= lookBackCount; j++)
            {
                if (i - j - 1 < 0) break; // предотвращаем -1 индекс
                avgDir += (Vector2)(segments[i - j].transform.position - segments[i - j - 1].transform.position).normalized;
                count++;
            }

            if (count > 0) avgDir /= count;

            // --- определяем фронт/бэк ---
            if (avgDir.y > 0f)
                segments[i].spriteRenderer.sprite = segments[i].backSide;
            else
                segments[i].spriteRenderer.sprite = segments[i].frontSide;
        }




        ApplyLengthConstraint();
        ApplyAngleConstraint();
    }

    void ApplyLengthConstraint()
    {
        for (int i = 1; i < segments.Length; i++)
        {
            Vector2 p0 = segments[i - 1].transform.position;
            Vector2 p1 = segments[i].transform.position;

            Vector2 dir = p1 - p0;
            float dist = dir.magnitude;
            if (dist < 0.0001f) continue;

            segments[i].transform.position = p0 + dir / dist * segmentLength;
        }
    }
    void ApplyAngleConstraint()
    {
        for (int i = 1; i < segments.Length; i++)
        {
            Vector2 baseDir =
                i == 1
                ? Vector2.down
                : ((Vector2)segments[i - 1].transform.position -
                   (Vector2)segments[i - 2].transform.position).normalized;

            Vector2 dir =
                ((Vector2)segments[i].transform.position -
                 (Vector2)segments[i - 1].transform.position).normalized;

            float angle = Vector2.SignedAngle(baseDir, dir);
            angle = Mathf.Clamp(angle, MinMaxAngle.x, MinMaxAngle.y);

            Vector2 finalDir =
                Quaternion.Euler(0, 0, angle) * baseDir;

            segments[i].transform.position =
                (Vector2)segments[i - 1].transform.position +
                finalDir * segmentLength;
        }
    }


}
