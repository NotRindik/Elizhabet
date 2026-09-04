using UnityEngine;
public abstract class BaseState : States.IState
{
    protected AbstractEntity owner;
    protected MonoBehaviour mono;

    public BaseState(AbstractEntity owner)
    {
        this.owner = owner;
        mono = (MonoBehaviour)owner;
    }

    public virtual void Update() { }
    public virtual void LateUpdate() { }

    public abstract void Enter();

    public abstract void Exit();
}