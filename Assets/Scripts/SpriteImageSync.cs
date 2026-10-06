using UnityEngine;
using UnityEngine.UI;

public class SpriteImageSync : MonoBehaviour
{
    private SpriteRenderer _sr;
    private Image _img;
    private Sprite _last;

    public void Init(SpriteRenderer sr, Image img)
    {
        _sr = sr;
        _img = img;
        Apply();
    }

    void LateUpdate()
    {
        if (_sr == null || _img == null) return;
        Apply();
    }

    void Apply()
    {
        var sprite = _sr.sprite;
        _img.enabled = _sr.enabled && sprite != null;
        _img.color = _sr.color;
        if (sprite == _last) return;
        _last = sprite;
        _img.sprite = sprite;
        if (sprite == null) return;
        var rt = _img.rectTransform;
        rt.pivot = sprite.pivot / sprite.rect.size;
        rt.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit;
    }
}