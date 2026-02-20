using System;
using Controllers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class TriggerEvent : MonoBehaviour
{
    
    public BetterEvent BetterEvent;
    public UnityEvent onTriggerEnter;
    public UnityEvent onTriggerExit;
    public UnityEvent onTriggerStay;
    public string localKey;
    public bool triggerOnce = true;
    private void Start()
    {
        if(SaveManager.Instance.GetModule<GlobalSaves>().Exist(WorldKeyBuilder.Build(this, localKey)) && triggerOnce)
            gameObject.SetActive(false);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController playerController))
        {
            onTriggerEnter?.Invoke();
            SaveManager.Instance.GetModule<GlobalSaves>().SetData(WorldKeyBuilder.Build(this, localKey),"1");
            SaveManager.Instance.SaveModule<GlobalSaves>();
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
