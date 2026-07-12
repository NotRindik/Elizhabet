using DG.Tweening;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;

public class DamageNumberManager : MonoBehaviour
{
    public void OnEnable()
    {
        EventBus.OnDamageApplied += DamageNumberUI;
    }

    public void OnDisable()
    {
        EventBus.OnDamageApplied -= DamageNumberUI;
    }

    private static void DamageNumberUI(HitInfo who)
    {
        if(who.Target.gameObject.layer == LayerMask.NameToLayer("Player"))
            return;

        if (!who.IsCrit)
        {
            WorldUIManager.Instance
                .Spawn("text", who.GetHitPos(), who.finalDmg.ToString())
                .FromBelow(8)
                .RandomX(12)
                .RandomRotate(5)
                .RandomMove(-15, 15, 40, 65, Ease.OutQuad)
                .Scale(0.75f, 1.5f)
                .PopEffect(1.15f, 0.08f)
                .FadeIn(0.06f)
                .FadeOut(0.25f)
                .Duration(0.5f)
                .Play()
                .SetFont("monogram-extended Bitmap");
        }
        else
        {
            WorldUIManager.Instance
                .Spawn("text", who.GetHitPos(), $"<color=yellow>{who.finalDmg}")
                .FromBelow(15)
                .RandomX(12)
                .RandomRotate(12)
                .Rotate(Random.Range(-8f, 8f), Random.Range(-25f, 25f))
                .RandomMove(-35, 35, 90, 130, Ease.OutBack)
                .Scale(0.45f, 3)
                .PopEffect(1.15f, 0.08f)
                .WithShake(12, 0.18f)
                .FadeIn(0.04f)
                .FadeOut(0.35f)
                .Duration(1)
                .Play()
                .SetFont("monogram-extended Bitmap");
        }
    }
}
