using UnityEngine;
using Systems;
using Assets.Scripts.Systems;
using System.Collections.Generic;
using Controllers;
public class PetsModification : BaseModificator, System.IDisposable
{
    private PetsModComponent penguinMC;
    private PetComponent petC;

    public void Dispose()
    {
        petC.petCountChange -= OnPatCountChange;
        ActiveStateChange -= OnActiveStateChange;
        if (IsActive)
            DestroyAllPenguins();
    }

    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);

        penguinMC = _modComponent.GetModComponent<PetsModComponent>();
        petC = owner.GetControllerComponent<PetComponent>();

        petC.petCountChange += OnPatCountChange;
        ActiveStateChange += OnActiveStateChange;

        OnActiveStateChange(IsActive);
    }

    private void OnActiveStateChange(bool active)
    {
        if (active)
        {
            SyncPenguinsToCount();
        }
        else
        {
            DestroyAllPenguins();
        }
    }

    private void OnPatCountChange(int count)
    {
        if (!IsActive)
            return;

        SyncPenguinsToCount();
    }

    private void SyncPenguinsToCount()
    {
        int target = petC.PetCount;
        int current = penguinMC.alivePet.Count;

        while (current < target)
        {
            AddPenguin();
            current++;
        }

        while (current > target)
        {
            RemovePenguin();
            current--;
        }
    }

    public void PenguinCoolDown(Controller died)
    {
        died.gameObject.SetActive(false);
        owner.StartCoroutine(std.Utilities.Invoke(() => {
            died.GetControllerSystem<HealthSystem>().HealToMax();
            died.gameObject.SetActive(true);
            died.transform.position = owner.transform.position;
        },petC.reSpawnCoolDown));
    }

    private void AddPenguin()
    {
        var inst = Object.Instantiate(petC.petPrefab, transform.position, Quaternion.identity);
        if(inst is IPoolAble pool) 
            pool.isPool = true;
        inst.GetControllerComponent<HealthComponent>().OnDie += PenguinCoolDown;
        inst.GetControllerComponent<FolowComponent>().folow = owner.transform;
        penguinMC.alivePet.Add(inst);
    }

    private void RemovePenguin()
    {
        int last = penguinMC.alivePet.Count - 1;
        penguinMC.alivePet[last].GetControllerComponent<HealthComponent>().OnDie -= PenguinCoolDown;
        Object.Destroy(penguinMC.alivePet[last].gameObject);
        penguinMC.alivePet.RemoveAt(last);
    }

    private void DestroyAllPenguins()
    {
        for (int i = penguinMC.alivePet.Count - 1; i >= 0; i--)
        {
            var peng = penguinMC.alivePet[i];
            if (peng != null)
            {
                peng.GetControllerComponent<HealthComponent>().OnDie -= PenguinCoolDown;
                Object.Destroy(peng.gameObject);
            }
        }
        penguinMC.alivePet.Clear();
    }
}

[System.Serializable]
public class PetsModComponent : IComponent
{
    public List<Controller> alivePet = new(3);
}

[System.Serializable]
public class PetComponent : IComponent
{
    public Controller petPrefab;
    [SerializeField] private int basePetCount = 1;
    private uint _petAdder = 0;
    public float reSpawnCoolDown = 4;
    public uint PetAdder 
    { 
        get 
        {
            return _petAdder;
        } 
        set 
        {
            _petAdder=value;
            petCountChange.Invoke(PetCount);
        } 
    }

    public int PetCount => (int)PetAdder + basePetCount;

    public System.Action<int> petCountChange;
}
