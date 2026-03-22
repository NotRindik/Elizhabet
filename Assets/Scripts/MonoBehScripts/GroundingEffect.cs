using Controllers;
using Systems;
using UnityEngine;

public class GroundingEffect : MonoBehaviour
{
    public ParticleSystem groundedParticle;
    private AbstractEntity entity;
    public EventSound audioEvent;

    private GroundingComponent groundingComponent;
    private TileDetectionComponent tdc;
    private Rigidbody2D rb;
    
    public AudioManagerWrapper audioWrapper;

    public bool isCrash;

    private float lastVelocityY;
    private float minCrashVelocity = -0.6f; // порог (подбери)
    public void Start()
    {
        entity = gameObject.GetComponent<AbstractEntity>();
        groundingComponent = entity.GetControllerComponent<GroundingComponent>();
        tdc = entity.GetControllerComponent<TileDetectionComponent>();
        groundingComponent.OnGround += OnGround;
        groundingComponent.OnUnGround += () => isCrash = false ;
        audioWrapper = GetComponent<AudioManagerWrapper>();
        rb = entity.GetControllerComponent<ControllersBaseFields>().rb;
        isCrash = true;
    }
    
    private void FixedUpdate()
    {
        if (!groundingComponent.IsReallyGrounded)
        {
            lastVelocityY = rb.linearVelocityY;
            Debug.Log($"Last velocity {lastVelocityY}");
        }
    }

    private void OnGround()
    {
        if (isCrash || lastVelocityY > minCrashVelocity)
            return;

        if (groundingComponent.groundedColliders.Length != 0)
        {
            var currTile = tdc.CurrTile;

            if (currTile)
            {
                var sprite = tdc.currTileData.sprite;

                if (groundedParticle != null)
                {
                    var textureSheetAnimation = groundedParticle.textureSheetAnimation;

                    if (textureSheetAnimation.GetSprite(0) != sprite)
                        textureSheetAnimation.SetSprite(0, sprite);
                }
            }
        }

        if (groundedParticle)
        {
            groundedParticle.Emit(30);

            if (audioEvent)
            {
                audioWrapper.PlaySoundEvent(audioEvent);
            }
        }

        isCrash = true;
    }

    public void OnDestroy()
    {
        groundingComponent.OnGround -= OnGround;
    }
}
