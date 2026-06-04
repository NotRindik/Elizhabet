using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class GlobalElectroLight : SerializedMonoBehaviour
{
    public bool isElecricityConnected = true;
    private GlobalSaves globalSaves;
    public UnityEvent<float> OnLightDataChanged;
    public UnityEvent OnLightDataExist;
    public UnityEvent OnConnectionOff;
    public Light2D[] light2D;
    public float intencityDelta;
    

    private Coroutine flickProcess;
    private void Start()
    {
        if (Application.isPlaying)
        {
            globalSaves = SaveManager.Instance.GetModule<GlobalSaves>();
            globalSaves.onGlobalStateChange += OnGlobalDataChange;
            OnConnectionOff.AddListener(ConnectionBreack);
            
            string key = "ElectroLightLevel";
            if (globalSaves.Exist(key))
            {
                SetIntencity(float.Parse(globalSaves.GetData(key)));
                OnLightDataExist?.Invoke();
            }
            else
            {
                SetIntencity(0);
            }
        }
        if (!isElecricityConnected)
        {
            SetIntencity(0);
            OnLightDataChanged.Invoke(0);
        }
    }

    private void ConnectionBreack() => OnGlobalDataChange("ElectroLightLevel", "0");

    public void ChangeElecricityConnection(bool val)
    {
        isElecricityConnected = val;
        if (!val)
            OnConnectionOff?.Invoke();
        else
            SetIntencity(1);
    }
    public void Update()
    {
        if (!Application.isPlaying)
        {
            SetIntencity();
        }
    }

    private void SetIntencity(float lightLevel = 1)
    {
        if (light2D != null)
        {
            for (int i = 0; i < light2D.Length; i++)
            {
                if (light2D[i] != null)
                {
                    light2D[i].intensity = Mathf.Max(lightLevel - intencityDelta, 0);
                }
            }
        }
    }

    public void OnGlobalDataChange(string key, string value)
    {
        if (key != "ElectroLightLevel")
            return;

        float lightLevel = float.Parse(value);
        if (isElecricityConnected == false)
            lightLevel = 0;

        if (lightLevel != 0)
        {
            if (flickProcess != null)
                StopCoroutine(flickProcess);
            flickProcess = StartCoroutine(Flick(15, lightLevel));
        }
        else
        {
            SetIntencity(lightLevel);
        }
        OnLightDataChanged.Invoke(lightLevel);
    }

    public IEnumerator Flick(float count,float finalVal,float flickSpeed = 0.1f)
    {
        while (count > 0)
        {
            float rngVal = Random.Range(0, finalVal);
            SetIntencity(rngVal);
            count -= 1;
            yield return new WaitForSeconds(flickSpeed);
        }

        SetIntencity(finalVal);
        flickProcess = null;
    }


    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            globalSaves.onGlobalStateChange -= OnGlobalDataChange;
            OnConnectionOff.RemoveAllListeners();
        }
    }
}
