using Systems;
using UnityEngine;

public class SurfaceDataProvider : MonoBehaviour, ISoundDataProvider
{
    private SurfaceDetectionComponent surfDetect;

    private void Start()
    {
        surfDetect = GetComponent<AbstractEntity>().GetControllerComponent<SurfaceDetectionComponent>();
    }

    public void Provide(EventSoundInstance instance)
    {
        if (surfDetect.CurrObject)
            return;
        if(!surfDetect.CurrObject.TryGetComponent(out AudioMaterialSetter mat))
            return;
        instance.SetData(new MaterialData()
        {
            material = mat.AudioMaterial
        });
    }
}
