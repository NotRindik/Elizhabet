using UnityEngine;

public class CompositeElectroLight : MonoBehaviour
{
    public GlobalElectroLight[] electroLight;

    public void OnValidate()
    {
        if(electroLight == null)
        {
            electroLight = GetComponentsInChildren<GlobalElectroLight>();
        }
    }

    public void SetElectricity(bool val)
    {
        foreach (var item in electroLight)
        {
            item.ChangeElecricityConnection(val);
        }
    }
}
