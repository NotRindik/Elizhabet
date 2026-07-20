using UnityEngine;

public class GameResourcesManager : MonoBehaviour, IGameService
{
    [SerializeField] private ItemsDataBase _itemsDataBase;
    public ItemsDataBase ItemsDataBase
    {
        get => _itemsDataBase;
        private set => _itemsDataBase = value;
    }

    public static GameResourcesManager Instance;

    public void Init()
    {
        if (Instance == null)
            Instance = this;
    }
}
