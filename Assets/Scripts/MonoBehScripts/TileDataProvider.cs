using System;
using Sirenix.OdinInspector;
using Systems;
using UnityEngine;

public class TileDataProvider : MonoBehaviour, ISoundDataProvider
{
    private TileDetectionComponent tile;
    public TileAudioDatabase tdb;

    private void Start()
    {
        tile = GetComponent<AbstractEntity>().GetControllerComponent<TileDetectionComponent>();
    }

    public void Provide(EventSoundInstance instance)
    {
        instance.SetData(new MaterialData()
        {
            material = tdb.Get(tile.CurrTile)
        });
    }
}


public interface ISoundDataProvider
{
    void Provide(EventSoundInstance instance);
}