using UnityEngine;

public class ScaleSyncer : MonoBehaviour
{
    public Transform sync;

    private void LateUpdate()
    {
        if(sync.localScale.x < 0)
        {
            transform.rotation = Quaternion.Euler(transform.rotation.x,-180,transform.rotation.z);
        }
        else
        {
            transform.rotation = Quaternion.Euler(transform.rotation.x, 0, transform.rotation.z);
        }
    }
}
