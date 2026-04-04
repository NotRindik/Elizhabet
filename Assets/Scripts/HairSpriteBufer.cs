using UnityEngine;

public class HairSpriteBufer : MonoBehaviour
{
    [HideInInspector] public SpriteRenderer spriteRenderer;
    public Sprite frontSide, backSide;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

}
