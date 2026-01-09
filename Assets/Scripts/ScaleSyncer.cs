using UnityEngine;

public class ScaleSyncer : MonoBehaviour
{
    public Transform sync;

    private void Update()
    {
        if(sync == null)
            return;
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