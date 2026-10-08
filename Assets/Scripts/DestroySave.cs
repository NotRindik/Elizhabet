using UnityEngine;

[DefaultExecutionOrder(1000)]
public class DestroySave : BoolStateObject
{
    protected override void OnLoaded() => gameObject.SetActive(false);

    public void SaveDestuction() => Save(true);
}