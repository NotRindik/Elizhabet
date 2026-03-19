using System;
using Systems;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EventDataBridge : MonoBehaviour
{
    public EventSound soundEvent;
    public AbstractEntity entiity;

    private SoundByTile _soundByTile;
    

    private void Start()
    {
        var tileDetectC = entiity.GetControllerComponent<TileDetectionComponent>();
        _soundByTile = soundEvent.GetMode<SoundByTile>();
        tileDetectC.OnTileChange += ProvideTile;
    }

    private void ProvideTile(TileBase curr)
    {
        _soundByTile.currTile = curr;
    }
}
