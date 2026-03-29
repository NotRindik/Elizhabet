using Systems;
using UnityEngine;

public class TileDataProvider : MonoBehaviour, ISoundDataProvider
{
    private TileDetectionComponent tile;
    public TileAudioDatabase tdb;
    public string interactionName;

    [SerializeField] private AbstractEntity entity;

    private void Start()
    {
        entity ??= GetComponent<AbstractEntity>();
        tile = entity.GetControllerComponent<TileDetectionComponent>();
    }

    public void Provide(EventSoundInstance instance)
    {
        instance.SetData(new MaterialData()
        {
            material = tdb.Get(tile.CurrTile),
            interaction =  interactionName,
        });
    }
}


public interface ISoundDataProvider
{
    void Provide(EventSoundInstance instance);
}