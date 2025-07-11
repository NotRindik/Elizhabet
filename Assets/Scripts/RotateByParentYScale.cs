using UnityEngine;

public class RotateByParentYScale : MonoBehaviour
{  
    
    void Update()
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, transform.parent.localScale.y == -1 ? 180f : 0f);

    }

}
