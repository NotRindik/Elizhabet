using Controllers;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class HairChain : MonoBehaviour
{
    public PlayerController controller;
    public HairChainData Chain;
    public Transform root;
    public HairSpriteBufer[] segments;

    public float segmentLength = 0.1f;
    public Vector2 gravity = new(0, -2f);

    [Sirenix.OdinInspector.MinMaxSlider(-180,180)]
    public Vector2 MinMaxAngle;

    Vector2 prevRoot;
    Vector2[] prev;

    int lookBackCount = 3;

    public SortingGroup playerSort, hairSort;
    
    private Transform[] t;
    private SpriteRenderer[] sr;

    public void OnEnable()
    {
        if (Chain != null)
            Chain.gameObject.SetActive(true);
    }

    public void OnDisable()
    {
        if (Chain != null && Chain.gameObject != null)
            Chain.gameObject.SetActive(false);
    }

    void Start()
    {
        if (Chain != null)
        {
            segments = Chain.segments;

            int len = segments.Length;

            t = new Transform[len];
            sr = new SpriteRenderer[len];
            prev = new Vector2[len];

            for (int i = 0; i < len; i++)
            {
                t[i] = segments[i].transform;
                sr[i] = segments[i].spriteRenderer;

                prev[i] = t[i].position;
            }

            prevRoot = root.position;

            Chain.transform.SetParent(null);
            Chain.GetComponent<SortingGroup>().sortingOrder = 13;
            DontDestroyOnLoad(Chain);
        }
    }

    void LateUpdate()
    {
        hairSort.sortingOrder = playerSort.sortingOrder - 1;

        float dt2 = Time.deltaTime * Time.deltaTime;

        Vector2 rootPos = root.position;
        Vector2 rootVel = rootPos - prevRoot;
        prevRoot = rootPos;

        t[0].position = rootPos;
        prev[0] = rootPos - rootVel;

        bool flip = controller.transform.localScale.x < 0;

        int len = t.Length;

        for (int i = 1; i < len; i++)
        {
            Transform ti = t[i];

            Vector2 cur = ti.position;
            Vector2 vel = cur - prev[i];
            prev[i] = cur;
            

            cur += gravity * dt2;
            ti.position = cur;

            // avgDir
            Vector2 avgDir = Vector2.zero;
            int count = 0;

            for (int j = 1; j <= lookBackCount; j++)
            {
                int a = i - j;
                int b = i - j - 1;

                if (b < 0) break;

                Vector2 dir = (Vector2)(t[a].position - t[b].position).normalized;
                avgDir += dir;
                count++;
            }

            if (count > 0)
                avgDir /= count;

            // sprite
            sr[i].sprite = avgDir.y > 0f
                ? segments[i].backSide
                : segments[i].frontSide;

            sr[i].flipX = flip;
        }

        ApplyLengthConstraint();
        ApplyAngleConstraint();
        ForceZ();
    }

    void ForceZ()
    {
        float z = root.position.z;
        int len = t.Length;

        for (int i = 0; i < len; i++)
        {
            Vector3 p = t[i].position;
            p.z = z;
            t[i].position = p;
        }
    }

    void ApplyLengthConstraint()
    {
        int len = t.Length;

        for (int i = 1; i < len; i++)
        {
            Vector2 p0 = t[i - 1].position;
            Vector2 p1 = t[i].position;

            Vector2 dir = p1 - p0;
            float dist = dir.magnitude;

            if (dist < 0.0001f) continue;

            t[i].position = p0 + dir / dist * segmentLength;
        }
    }

    void ApplyAngleConstraint()
    {
        int len = t.Length;

        float min = MinMaxAngle.x;
        float max = MinMaxAngle.y;

        for (int i = 1; i < len; i++)
        {
            Vector2 baseDir =
                i == 1
                ? Vector2.down
                : ((Vector2)(t[i - 1].position - t[i - 2].position)).normalized;

            Vector2 dir =
                ((Vector2)(t[i].position - t[i - 1].position)).normalized;

            float angle = Vector2.SignedAngle(baseDir, dir);
            angle = Mathf.Clamp(angle, min, max);

            Vector2 finalDir =
                Quaternion.Euler(0, 0, angle) * baseDir;

            t[i].position =
                (Vector2)t[i - 1].position +
                finalDir * segmentLength;
        }
    }
}