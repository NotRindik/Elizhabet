using UnityEngine;
using Systems;
using Assets.Scripts.Systems;
using System.Collections.Generic;
using Controllers;
public class PenguinMod : BaseModificator
{
    private PenguinModComponent penguinMC;
    private PetComponent petC;

    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        penguinMC = _modComponent.GetModComponent<PenguinModComponent>();
        petC = _modComponent.GetModComponent<PetComponent>();
        owner.OnUpdate += Update;
    }
    public override void OnUpdate()
    {
        base.OnUpdate();


    }
}

public class PenguinModComponent : IComponent
{
    public List<Penguino> alivePenguinos = new(3);
    public Penguino penguinPrefab;
}

public class PetComponent : IComponent
{
    [SerializeField] private int basePetCount = 1;
    public uint petAdder = 0;

    public int PetCount => (int)petAdder + basePetCount;
}
