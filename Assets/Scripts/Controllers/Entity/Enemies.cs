using Controllers;
using Systems;
using UnityEngine;

public class Enemies : OptimizedController
{
    protected override IComponent[] DefaultComponents => new IComponent[]
    {
        new ControllersBaseFields
        {
            collider = GetComponents<Collider2D>(),
            rb = GetComponent<Rigidbody2D>(),
        },
        new AnimationComponent
        {
            animator = GetComponentInChildren<Animator>()
        },
        new FsmComponent(),
        new SimpleMoveComponent
        {
            speed = 1,
            speedMultiplier = 1
        }
    };

    protected override ISystem[] DefaultSystems => new ISystem[]
    {
        new SimpleMoveSystem(),
        new FSMSystem()
    };
}
