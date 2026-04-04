using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class SelfDestruction : SerializedMonoBehaviour
{
    public float destructTime;

    public DestructionType destructionType;

    public void Destruct()
    {
        destructionType.Destruct(this);
    }
}

public interface DestructionType
{
    public void Destruct(SelfDestruction slf);
}

public class SpriteDisappear : DestructionType
{
    public SpriteRenderer renderer;
    public float delay;
    public void Destruct(SelfDestruction slf)
    {
        renderer ??= slf.GetComponent<SpriteRenderer>();
        renderer
            .DOColor(new Color(0, 0, 0, 0), slf.destructTime-delay)
            .SetDelay(delay);
        Object.Destroy(slf.gameObject,slf.destructTime);
    }
}