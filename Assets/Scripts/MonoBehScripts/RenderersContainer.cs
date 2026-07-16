using UnityEngine;

public class RenderersContainer : MonoBehaviour
{
    [SerializeField] public SpriteRenderer[] renderers;
    
    private void OnValidate()
    {
        if(renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>();
    }
}
