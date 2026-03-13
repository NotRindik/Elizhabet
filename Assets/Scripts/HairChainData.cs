using UnityEngine;

public class HairChainData : MonoBehaviour
{
    public HairSpriteBufer[] segments;

    public void SetEnable(bool enabled)
    {
        gameObject.SetActive(enabled);
    }
}
