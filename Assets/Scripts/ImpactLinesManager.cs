using System;
using DG.Tweening;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;

public class ImpactLinesManager : MonoBehaviour
{
    [Header("Наборы спрайтов")]
    [SerializeField] private Sprite[] playerImpactSprites;
    [SerializeField] private Sprite[] mobImpactSprites;
    [SerializeField] private Sprite[] critImpactSprites;

    private void OnEnable()  => EventBus.OnDamageApplied += Impact;
    private void OnDisable() => EventBus.OnDamageApplied -= Impact;

    private void Impact(HitInfo who)
    {
        Sprite[] set = SelectSpriteSet(who);
        if (set == null || set.Length == 0)
        {
            Debug.LogWarning($"[{name}] нет спрайтов импакта для этого случая (crit={who.IsCrit}, target={who.Target?.mono?.name})");
            return;
        }

        Sprite sprite = set[Random.Range(0, set.Length)];
        float angle = GetImpactAngle(who);

        var builder = WorldUIManager.Instance
            .Spawn("image", who.GetHitPos())
            .Duration(0.1f);

        if (who.IsCrit)
            builder.WithShake(12, 0.18f);

        builder
            .Rotate(angle)
            .Play()
            .SetSprite(sprite)
            .SetNativeSize();
    }

    private Sprite[] SelectSpriteSet(HitInfo who)
    {
        if (who.IsCrit)
            return critImpactSprites;

        bool isPlayer = who.Target.gameObject.layer == LayerMask.NameToLayer("Player");
        return isPlayer ? playerImpactSprites : mobImpactSprites;
    }

    private float GetImpactAngle(HitInfo who)
    {
        if (who.AttackVelocity.sqrMagnitude < 0.0001f)
            return Random.Range(0f, 360f);

        return Mathf.Atan2(who.AttackVelocity.y, who.AttackVelocity.x) * Mathf.Rad2Deg;
    }
}
