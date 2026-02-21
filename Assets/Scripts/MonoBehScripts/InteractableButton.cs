using Systems;
using UnityEngine;

public class Button : MonoBehaviour, IInteractable
{
    public BetterEvent onInteract;

    public void Interact(AbstractEntity interactor)
    {
        if(!enabled)
            return;

        onInteract.Invoke();
    }

    public void isActive(bool isActive)
    {
        this.enabled = isActive;
    }
}
