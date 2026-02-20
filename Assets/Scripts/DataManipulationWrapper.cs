using System.Collections;
using UnityEngine;

public class DataManipulationWrapper : MonoBehaviour
{
    public void SetGlobal(string data)
    {
        string[] kvp = data.Split(',');

        SaveManager.Instance.GetModule<GlobalSaves>().SetData(kvp[0], kvp[1]);
        SaveManager.Instance.SaveModule<GlobalSaves>();
    }

}
