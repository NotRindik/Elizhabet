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
        },
        new VisionComponent {
            forgetTime = 10,
            filters = new IVisionFilter[]
            {
                new FilterByLineOfSight{obstacleLayer = 6,owner = this},
                new FilterByViewAngle{owner = this,viewAngle = 90}
            },
        },
        new HealthComponent
        {
            currHealth = 10,
            maxHealth = 10,
        }
    };

    protected override ISystem[] DefaultSystems => new ISystem[]
    {
        new SimpleMoveSystem(),
        new FSMSystem(),
        new VisionMemorySystem(),
        new HealthSystem(),
    };
}
