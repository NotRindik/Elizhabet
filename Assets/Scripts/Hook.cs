using System;
using UnityEngine;

public class Hook : MonoBehaviour
{
    public Sprite hookTip;

    public LineRenderer hookRenderer;
    private SpriteRenderer hookTipSpriteRender;

    private void Start()
    {
        hookRenderer = GetComponent<LineRenderer>();
        hookTipSpriteRender = new GameObject("HookTip",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        hookTipSpriteRender.sprite = hookTip;
        hookTipSpriteRender.sortingOrder = hookRenderer.sortingOrder + 1;
    }

    private void LateUpdate()
    {
        Vector3 start = hookRenderer.GetPosition(0);
        Vector3 end = hookRenderer.GetPosition(1);
        Vector3 dir = end - start;

        hookTipSpriteRender.transform.position = end;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        hookTipSpriteRender.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180);
        hookTipSpriteRender.transform.position = (transform.position + hookRenderer.GetPosition(1));
    }

    private void OnDestroy()
    {
        Destroy(hookTipSpriteRender.gameObject);
    }
}
