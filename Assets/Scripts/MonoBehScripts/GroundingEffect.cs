using Systems;
using UnityEngine;

public class GroundingEffect : MonoBehaviour
{
    public ParticleSystem groundedParticle;
    private AbstractEntity entity;
    public EventSound audioEvent;

    private GroundingComponent groundingComponent;
    private TileDetectionComponent tdc;

    public bool isCrash;


    public void Start()
    {
        entity = gameObject.GetComponent<AbstractEntity>();
        groundingComponent = entity.GetControllerComponent<GroundingComponent>();
        tdc = entity.GetControllerComponent<TileDetectionComponent>();
        groundingComponent.OnGround += OnGround;
        groundingComponent.OnUnGround += () => isCrash = false ;
        isCrash = true;
    }

    private void OnGround()
    {
        if (isCrash == true)
            return;
        if (groundingComponent.groundedColliders.Length != 0)
        {
            var currTile = tdc.currTile;

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
            if(audioEvent) AudioManager.instance.PlayEvent(new EventSoundInstance(audioEvent));
        }

        isCrash = true;
    }

    public void OnDestroy()
    {
        groundingComponent.OnGround -= OnGround;
    }
}
