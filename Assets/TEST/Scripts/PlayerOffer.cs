using UnityEngine;

public class PlayerOffer : MonoBehaviour
{
    void Start()
    {
        InputManager.inputActions.DevMap.Test.performed += _ =>
        {
            gameObject.SetActive(!gameObject.activeInHierarchy);
        };
    }
    
}
