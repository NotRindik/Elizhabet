using System;
using UnityEngine;


namespace Systems
{
    public class ItemContainer : MonoBehaviour,IInteractable
    {
        private ItemsDataBase DataBase;

        public Item[] itemsToSpawn;
        private void Start()
        {
            DataBase = GameResourcesManager.Instance.ItemsDataBase;
        }
        public void Interact(AbstractEntity interactor)
        {
            var inventory = interactor.GetControllerSystem<InventorySystem>();
            int i = UnityEngine.Random.Range(0, itemsToSpawn.Length);

            if (!inventory.CanAcceptItem(itemsToSpawn[i].name))
            {
                NotflicationManager.Instance.Send("Inventory Full");
                return;
            }
            var item = Instantiate(itemsToSpawn[i],transform.position,Quaternion.identity);
            inventory.SetItem(item);
        }
        public bool CanInteract(AbstractEntity _) => isActiveAndEnabled;
    }

    public interface IInteractable
    {
        public void Interact(AbstractEntity interactor);

        public bool CanInteract(AbstractEntity entity);
    }
}