using UnityEngine;

public class DisableInPlay : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(false);
    }
}
