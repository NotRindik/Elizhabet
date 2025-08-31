using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ProjectileController : EntityController
{
    private CustomGravitySystem gravitySystem = new CustomGravitySystem();

    public CustomGravityComponent gravityComponent;
    public ProjectileComponent projectileComponent;
    public ParticleSystem groundParticlePrefab;
    public void Start()
    {
        Destroy(gameObject, projectileComponent.lifetime);
    }

    public override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);
        if (((1 << collision.gameObject.layer) & projectileComponent.hitLayer.value) != 0)
        {
            if (collision.gameObject.TryGetComponent(out Controller controller))
            {
                var hpSys = controller.GetControllerSystem<HealthSystem>();
                hpSys.TakeHit(projectileComponent.damage, collision.contacts[0].point);
            }
            else if (collision.gameObject.TryGetComponent(out TilemapCollider2D tilemapCollider))
            {
                // получаем сам tilemap
                var tilemap = tilemapCollider.GetComponent<Tilemap>();
                if (tilemap == null) return;

                // берём точку удара
                Vector2 hitPoint = collision.contacts[0].point;

                Vector2 insidePoint = (Vector2)hitPoint - collision.contacts[0].normal * 0.05f;

                // переводим в cell-координаты
                Vector3Int cellPos = tilemap.WorldToCell(insidePoint);

                // достаём тайл
                TileBase tile = tilemap.GetTile(cellPos);
                if (tile != null)
                {
                    Sprite tileSprite = tile is Tile t ? t.sprite : tilemap.GetSprite(cellPos);

                    EmitParitcle(collision, hitPoint, tileSprite);
                }
            }
            else if (collision.gameObject.TryGetComponent(out SpriteRenderer sr))
            {
                Vector2 hitPoint = collision.contacts[0].point;
                EmitParitcle(collision, hitPoint, sr.sprite);
            }
            healthSystem.TakeHit(1, collision.contacts[0].point);
        }
    }

    private void EmitParitcle(Collision2D collision, Vector2 hitPoint, Sprite tileSprite)
    {
        var particleInstance = Instantiate(
            groundParticlePrefab,
            hitPoint,
            Quaternion.identity
        );

        var textureSheet = particleInstance.textureSheetAnimation;
        textureSheet.SetSprite(0, tileSprite);

        float speed = collision.relativeVelocity.magnitude/2;
        int emitCount = Mathf.Clamp(Mathf.RoundToInt(speed), 1, 100);

        particleInstance.Emit(emitCount);
    }
}

[System.Serializable]
public class ProjectileComponent : IComponent
{
    public float lifetime;
    public float damage;
    public LayerMask hitLayer;

    public ProjectileComponent(float lifeTime, float damage, LayerMask hitLayer)
    {
        this.lifetime = lifeTime;
        this.damage = damage;
        this.hitLayer = hitLayer;
    }
}
