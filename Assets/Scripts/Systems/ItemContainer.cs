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
            DataBase = Bootstrap.instance.itemDB;
        }
        public void Interact(AbstractEntity interactor)
        {
            var inventory = interactor.GetControllerSystem<InventorySystem>();
            int i = UnityEngine.Random.Range(0, itemsToSpawn.Length);
            if (inventory.IsFullStack(itemsToSpawn[i]))
                return;

            var item = Instantiate(itemsToSpawn[i],transform.position,Quaternion.identity);
            inventory.SetItem(item);
        }
    }

    public interface IInteractable
    {
        public void Interact(AbstractEntity interactor);
    }
}