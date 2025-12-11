using Systems;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    public static Bootstrap instance;
    private static Bootstrap Instance { get { return instance; } set { instance = value; } }

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(Instance.gameObject);
            Instance = this;
        }
    }
}