
using System;
using System.Collections;
using std;
using Systems;
using UnityEngine;

public class TR1Component : IComponent
{
    public SpriteRenderer line,attackSprite,BaseSprite;
    public float attackDistance = 5f;
    public float attackSpeed = 10f;

    public Sprite[] RotAnimation;
} 


public class TR1AI : BaseAI
{
    private TargetSearchComponent trc;
    private TR1Component tr1;

    private Action<InputContext> attackAction;
    private std.Optional<Coroutine> attackRoutine;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());

        trc = owner.GetControllerComponent<TargetSearchComponent>();
        tr1 = owner.GetControllerComponent<TR1Component>();

        trc.onTargetChange += OnFindTarget;

        attackAction += context =>
        {
            Debug.Log(attackRoutine.Enabled);
            if (!attackRoutine.Enabled)
            {
                attackRoutine = Optional<Coroutine>.Some(owner.StartCoroutine(AttackRoutine()));
            }
        };

        GetState().Attack.performed += attackAction;
    }

    public void OnFindTarget(Transform target)
    {
        Debug.Log("Change");
        GetState().Attack.Update(target != null, true);
    }

    private IEnumerator AttackRoutine()
    {
        Vector3 startSize = tr1.line.size;
        Vector3 startPos = tr1.attackSprite.transform.localPosition;
        float t = 0f;
        int i = 0;
        int len = tr1.RotAnimation.Length;

        // Расширение линии вверх
        while (t < 1f)
        {
            t += Time.deltaTime * tr1.attackSpeed;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            tr1.line.size = new Vector3(startSize.x, Mathf.Lerp(0f, tr1.attackDistance, eased), 1f);
            tr1.attackSprite.transform.localPosition = startPos + Vector3.up * (tr1.line.size.y - startPos.y);

            // Анимация спрайта по кругу
            tr1.BaseSprite.sprite = tr1.RotAnimation[i];
            i = (i + 1) % len;

            yield return null;
        }

        t = 0f;

        // Сжатие линии обратно
        while (t < 1f)
        {
            t += Time.deltaTime * tr1.attackSpeed;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            tr1.line.size = new Vector3(startSize.x, Mathf.Lerp(tr1.attackDistance, 0f, eased), 1f);
            tr1.attackSprite.transform.localPosition = startPos + Vector3.up * (tr1.line.size.y - startPos.y);

            // Анимация спрайта по кругу назад
            tr1.BaseSprite.sprite = tr1.RotAnimation[i];
            i = (i - 1 + len) % len; // безопасно, не выйдет за границу

            yield return null;
        }

        // Сброс позиции и размера
        tr1.line.size = startSize;
        tr1.attackSprite.transform.localPosition = startPos;
        attackRoutine = Optional<Coroutine>.None();
    }
}
public class RayTargetSerch : BaseSystem
{
    private TargetSearchComponent trc;
    private RaycastHit2D[] _hits;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        trc = owner.GetControllerComponent<TargetSearchComponent>();
        _hits = new RaycastHit2D[10];
        owner.OnUpdate += Update;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        var hit = Physics2D.RaycastNonAlloc(transform.position,transform.up,_hits,trc.searchRadius,trc.targetLayer);

        if (hit == 0 && trc.CurrentTarget != null)
            trc.CurrentTarget = null;
        
        for (int i = 0; i < hit; i++)
        {
            if (_hits[i].collider.TryGetComponent(out AbstractEntity entity))
            {
                trc.CurrentTarget = entity.transform;
                return;
            } 
        }
    }
}