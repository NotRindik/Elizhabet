using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class ProjectileController : EntityController
{
    private CustomGravitySystem gravitySystem = new CustomGravitySystem();

    public CustomGravityComponent gravityComponent;
    public ProjectileComponent projectileComponent;
    public WeaponComponent weaponComponent;
    public ParticleSystem groundParticlePrefab;
    public float curr;
    public bool isHit;

    public Vector2 firstContactVel;
    public void Start()
    {
        Destroy(gameObject, projectileComponent.lifetime);
        curr = projectileComponent.lifetime;
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        curr -= Time.deltaTime;
        float t = 1f - (curr / projectileComponent.lifetime);

        baseFields.rb.gravityScale = isHit == false ? Mathf.Lerp(0,0.5f,t) : 0.5f;
    }

    public override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);
        var contact = collision.GetContact(0);
        Vector2 velocity = baseFields.rb.linearVelocity;
        Vector2 reflected = Vector2.Reflect(transform.right, contact.normal);

        baseFields.rb.linearVelocity = reflected * velocity.magnitude/1.2f;
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(reflected.y, reflected.x) * Mathf.Rad2Deg);
        gravityComponent.gravityVector = Vector2.zero;
        transform.localScale *= 0.5f;

        isHit = true;
        if (((1 << collision.gameObject.layer) & weaponComponent.attackLayer.value) != 0)
        {
            if (collision.gameObject.TryGetComponent(out Controller controller))
            {
                baseFields.rb.linearVelocity /= 2.3f;
                var hpSys = controller.GetControllerSystem<HealthSystem>();

                var protectionComponent = controller.GetControllerComponent<ProtectionComponent>();

                if (baseFields.rb.linearVelocity.magnitude > 0.5f)
                {
                    DamageComponent dmg = weaponComponent.modifiedDamage;
                    new Damage(dmg, protectionComponent).ApplyDamage(hpSys, new HitInfo(collision.contacts[0].point));
                }
            }
            healthSystem.TakeHit(new HitInfo(controller,1));
        }
        else if (((1 << collision.gameObject.layer) & projectileComponent.destroyLayer.value) != 0) 
        {
            if (collision.gameObject.TryGetComponent(out TilemapCollider2D tilemapCollider))
            {
                var tilemap = tilemapCollider.GetComponent<Tilemap>();
                if (tilemap == null) return;

                Vector2 hitPoint = collision.contacts[0].point;

                Vector2 insidePoint = (Vector2)hitPoint - collision.contacts[0].normal * 0.05f;

                Vector3Int cellPos = tilemap.WorldToCell(insidePoint);

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

            healthSystem.TakeHit(new HitInfo(collision.contacts[0].point,1));
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
        var main = particleInstance.main;
        main.stopAction = ParticleSystemStopAction.Destroy;
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
