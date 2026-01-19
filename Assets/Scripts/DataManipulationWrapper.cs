using System.Collections;
using UnityEngine;

public class DataManipulationWrapper : MonoBehaviour
{
    public void SetGlobal(string data)
    {
        string[] kvp = data.Split(',');
        bool imidiate = true;

        if (kvp.Length > 2)
            imidiate = bool.Parse(kvp[2]);

        if (imidiate)
            SaveManager.Instance.GetModule<GlobalSaves>().SetData(kvp[0], kvp[1]);
        else
        {
            StartCoroutine(SetProcess(kvp[0], int.Parse(kvp[1])));
        }
    }

    public IEnumerator SetProcess(string k,float maxV = 0)
    {
        float curr = 0;
        while (curr < maxV)
        {
            curr = float.Parse(SaveManager.Instance.GetModule<GlobalSaves>().GetData(k));
            curr = Mathf.MoveTowards(curr,maxV,0.001f);
            SaveManager.Instance.GetModule<GlobalSaves>().SetData(k, curr.ToString());
            yield return new WaitForSeconds(0.1f);
        }
    }
}
