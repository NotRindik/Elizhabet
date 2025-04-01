using System;
using UnityEngine;
using UnityEngine.UIElements;

public class HealthUIItem : MonoBehaviour
{
    [SerializeField] public Image image;

    public void Init()
    {
        if (image == null)
        {
            image = GetComponentInChildren<Image>();
        }
    }

    public void OnUpdate()
    {

    }
}
