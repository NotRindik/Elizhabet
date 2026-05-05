using System;
using System.Runtime.CompilerServices;
using Controllers;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class TriggerEvent : MonoBehaviour
{
    public UnityEvent onTriggerEnter;
    public UnityEvent onTriggerExit;
    public UnityEvent onTriggerStay;
    public string localKey;
    public bool triggerOnce = true;

    [ToggleGroup("AdvancedTriggres")]
    public bool AdvancedTriggres;

    [ToggleGroup("AdvancedTriggres")]
    public BetterEvent onTriggerEnterAdv;
    [ToggleGroup("AdvancedTriggres")]
    public BetterEvent onTriggerExitAdv;
    [ToggleGroup("AdvancedTriggres")]
    public BetterEvent onTriggerStayAdv;


    private void Start()
    {
        if(SaveManager.Instance.GetModule<WorldObjectsStateSave>().Exist(WorldKeyBuilder.Build(this, localKey)) && triggerOnce)
            gameObject.SetActive(false);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController playerController))
        {
            onTriggerEnter?.Invoke();
            onTriggerEnterAdv.Invoke();
            SaveManager.Instance.GetModule<WorldObjectsStateSave>().SetData(WorldKeyBuilder.Build(this, localKey),"1").SaveModule<WorldObjectsStateSave>();
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController playerController))
        {
            onTriggerExit?.Invoke();
            onTriggerExitAdv.Invoke();
        }
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController playerController))
        {
            onTriggerStay?.Invoke();
            onTriggerStayAdv.Invoke();
        }
    }
}
