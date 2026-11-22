using System;
using Sirenix.OdinInspector;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class DestroySave : BoolStateObject
{
    protected override void OnLoaded() => gameObject.SetActive(false);

    public void SaveDestuction() => Save(true);

    public bool AutoSaveOnDie = true;

    private void OnEnable()
    {
         
    }

    private void OnDisable()
    {
        
    }
}