using System;
using Systems;
using UnityEngine;

public class SpriteSynchronizer : MonoBehaviour,IComponent
{
    public SpriteRenderer mainSpriteRender;
    public SpriteRenderer hairSprire;

    private void Start()
    {
        hairSprire = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        hairSprire.sprite = mainSpriteRender.sprite;
        hairSprire.transform.position = mainSpriteRender.transform.position;
        hairSprire.transform.localScale = mainSpriteRender.transform.localScale;
        hairSprire.transform.eulerAngles = mainSpriteRender.transform.eulerAngles;
    }
}
