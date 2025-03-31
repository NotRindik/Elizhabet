using UnityEngine;

public class HealthUIItem : MonoBehaviour
{
    public SpriteRenderer sprite;


    private void OnValidate()
    {
        if (sprite == null)
        {
            sprite = GetComponent<SpriteRenderer>();
        }
    }

    public void OnUpdate()
    {

    }
}
