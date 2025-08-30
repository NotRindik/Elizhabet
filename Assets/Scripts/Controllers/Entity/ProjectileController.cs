using Controllers;
using Systems;
using UnityEngine;

public class ProjectileController : EntityController
{
    private CustomGravitySystem gravitySystem = new CustomGravitySystem();

    public CustomGravityComponent gravityComponent;
    public ProjectileComponent projectileComponent;

    public void Start()
    {
        Destroy(gameObject, projectileComponent.lifetime);
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & projectileComponent.hitLayer.value) != 0)
            Destroy(gameObject);
    }
}

[System.Serializable]
public class ProjectileComponent : IComponent
{
    public float lifetime;
    public LayerMask hitLayer;
}
