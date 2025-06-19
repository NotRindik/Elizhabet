using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TextPixel : MonoBehaviour
{
    [SerializeField] private Transform target;
    private void OnEnable()
    {
        Text();
    }
    void Update()
    {
        Text();
    }
    void Text()
    {
        transform.position = Camera.main.WorldToScreenPoint(new Vector3(target.position.x, target.position.y, target.position.z));
    }
}