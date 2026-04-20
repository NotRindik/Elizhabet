using System;
using Systems;
using UnityEngine;

public class Button : MonoBehaviour, IInteractable
{
    public BetterEvent onInteract;

    [Header("Move settings")]
    public Transform interactionPoint;
    public float stopDistance = 0.1f;
    public float maxSpeed = 2f;

    private AbstractEntity currentInteractor;
    private AIMoveInput moveAI;
    private bool isBusy;

    public void Interact(AbstractEntity interactor)
    {
        if (!enabled || isBusy)
            return;

        var proxyInput = interactor.GetControllerSystem<ProxyInputState>();

        currentInteractor = interactor;
        isBusy = true;

        moveAI = new AIMoveInput
        {
            target = interactionPoint != null ? interactionPoint : transform,
            stopDistance = stopDistance,
            maxSpeed = maxSpeed
        };

        moveAI.Initialize(interactor);
        moveAI.SetState(proxyInput.GetState());

        moveAI.OnTargetReached += OnReached;
        moveAI.OnEmergencyShutdown += Cancel;

        proxyInput.SetProvider(moveAI);
    }

    private void OnReached()
    {

        onInteract.Invoke();

        // вернуть управление игроку
        if (currentInteractor != null)
        {
            Debug.Log("ComeBack");
            var proxy = currentInteractor.GetControllerSystem<ProxyInputState>();
            var input = new PlayerSourceInput();
            proxy.SetProvider(input);
        }
        Cleanup();
    }

    private void Cancel()
    {
        Cleanup();

        if (currentInteractor != null)
        {
            var proxy = currentInteractor.GetControllerSystem<ProxyInputState>();
            proxy.SetProvider(new PlayerSourceInput());
        }
    }

    private void Cleanup()
    {
        if (moveAI != null)
        {
            moveAI.OnTargetReached -= OnReached;
            moveAI.OnEmergencyShutdown -= Cancel;
            (moveAI as IDisposable)?.Dispose();
        }

        moveAI = null;
        currentInteractor = null;
        isBusy = false;
    }

    public bool CanInteract(AbstractEntity _) => isActiveAndEnabled && !isBusy;
}