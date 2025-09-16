using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ProjectileController : EntityController
{
    private CustomGravitySystem gravitySystem = new CustomGravitySystem();

    public CustomGravityComponent gravityComponent;
    public ProjectileComponent projectileComponent;
    public WeaponComponent weaponComponent;
    public ParticleSystem groundParticlePrefab;
    public void Start()
    {
        Destroy(gameObject, projectileComponent.lifetime);
    }

    public override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);
        if (((1 << collision.gameObject.layer) & weaponComponent.attackLayer.value) != 0)
        {
            if (collision.gameObject.TryGetComponent(out Controller controller))
            {
                var hpSys = controller.GetControllerSystem<HealthSystem>();
                var protectionComponent = controller.GetControllerComponent<ProtectionComponent>();
                new Damage(weaponComponent.modifiedDamage, protectionComponent).ApplyDamage(hpSys, new HitInfo(collision.contacts[0].point));
            }
            //healthSystem.TakeHit(1, collision.contacts[0].point);
        }
        else if (((1 << collision.gameObject.layer) & projectileComponent.destroyLayer.value) != 0) 
        {
            if (collision.gameObject.TryGetComponent(out TilemapCollider2D tilemapCollider))
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
            Destroy(gameObject);
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
public struct ProjectileComponent : IComponent
{
    public float lifetime;
    public LayerMask destroyLayer;

    public ProjectileComponent(float lifeTime,LayerMask destroyLayer)
    {
        this.lifetime = lifeTime;
        this.destroyLayer = destroyLayer;
    }
}
