using Systems;
using UnityEngine;

public class Button : MonoBehaviour, IInteractable
{
    public BetterEvent onInteract;

    public void Interact(AbstractEntity interactor)
    {
        onInteract.Invoke();
    }
}
