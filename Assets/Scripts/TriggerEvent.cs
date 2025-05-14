using System;
using Controllers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class TriggerEvent : MonoBehaviour
{
    
    public UnityEvent onTriggerEnter;
    public UnityEvent onTriggerExit;
    public UnityEvent onTriggerStay;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController playerController))
        {
            onTriggerEnter?.Invoke();
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController playerController))
        {
            onTriggerExit?.Invoke();
        }
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController playerController))
        {
            onTriggerStay?.Invoke();
        }
    }
}
