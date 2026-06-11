using System;
using UnityEngine;

public class UICamera : MonoBehaviour
{
    public Camera uiCamera;
    public static UICamera Instance;

    public static Action<UICamera> OnInited;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            OnInited?.Invoke(Instance);
        }
    }
}
