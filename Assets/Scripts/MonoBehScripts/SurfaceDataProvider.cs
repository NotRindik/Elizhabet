using Systems;
using UnityEngine;

public class SurfaceDataProvider : MonoBehaviour, ISoundDataProvider
{
    private SurfaceDetectionComponent surfDetect;
    [SerializeField] private AbstractEntity entity;
    [SerializeField] private string interactionName;

    private void Start()
    {
        entity ??= GetComponent<AbstractEntity>();
        surfDetect = entity.GetControllerComponent<SurfaceDetectionComponent>();
    }

    public void Provide(EventSoundInstance instance)
    {
        if (!surfDetect.CurrObject)
            return;
        if(!surfDetect.CurrObject.TryGetComponent(out AudioMaterialSetter mat))
            return;
        instance.SetData(new MaterialData()
        {
            material = mat.AudioMaterial,
            interaction = interactionName
        });
    }
}
