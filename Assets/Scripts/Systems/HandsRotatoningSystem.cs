using AYellowpaper.SerializedCollections;
using Controllers;
using Systems;
using UnityEngine;

public class HandsRotatoningSystem : BaseSystem
{
    private HandsRotatoningComponent _handRotatoning;
    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        _handRotatoning = owner.GetControllerComponent<HandsRotatoningComponent>();
    }

    public void RotateHand(Side side, Vector2 pos)
    {
        _handRotatoning.handRotatoning[side].RotateHand(pos);
    }
}

[System.Serializable]
public class HandsRotatoningComponent : IComponent
{
    public SerializedDictionary<Side,HandRotatoning> handRotatoning;
}

public enum Side
{
    Left, Right
}