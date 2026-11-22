using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class SaveIconView : SerializedMonoBehaviour
{
    enum Phase { None, Show, Idle, Hide }

    public CanvasGroup group;
    public Animator animator;
    [MinMaxSlider(1,20)]public Vector2Int showRange, idleRange, hideRange;

    [Min(0f)] public float crossFade = 0.1f;
    [Min(0f)] public float idleDuration = 1.5f;
    [Min(0f)] public float maxStateDuration = 5f;
    public int layer;
    public bool unscaledTime = true;
    public bool onlySuccessful = true;

    Phase phase;
    int stateHash;
    float timer;

    void Awake()
    {
        if (animator != null && unscaledTime)
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        SetVisible(false,true);
    }

    void OnEnable()
    {
        SaveManager.Instance.Saved += OnSaved;
    }

    void OnDisable()
    {
        SaveManager.Instance.Saved -= OnSaved;
        phase = Phase.None;
        SetVisible(false);
    }

    void OnSaved(bool ok)
    {
        if (onlySuccessful && !ok)
            return;
        Play();
    }

    public void Play()
    {
        switch (phase)
        {
            case Phase.None:
            case Phase.Hide:
                StartShow();
                break;
            case Phase.Idle:
                timer = idleDuration;
                break;
        }
    }

    void Update()
    {
        if (phase == Phase.None)
            return;

        timer -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        switch (phase)
        {
            case Phase.Show:
                if (Finished())
                    StartIdle();
                break;
            case Phase.Idle:
                if (timer <= 0f)
                    StartHide();
                break;
            case Phase.Hide:
                if (Finished())
                {
                    phase = Phase.None;
                    SetVisible(false);
                }
                break;
        }
    }

    void StartShow()
    {
        SetVisible(true);
        phase = Phase.Show;
        timer = maxStateDuration;
        PlayRandom("Show", showRange, false);
    }

    void StartIdle()
    {
        phase = Phase.Idle;
        timer = idleDuration;
        PlayRandom("Idle", idleRange, true);
    }

    void StartHide()
    {
        phase = Phase.Hide;
        timer = maxStateDuration;
        PlayRandom("Hide", hideRange, true);
    }

    void PlayRandom(string prefix, Vector2Int range, bool fade)
    {
        var min = Mathf.Min(range.x, range.y);
        var max = Mathf.Max(range.x, range.y);
        var name = prefix + Random.Range(min, max + 1);
        stateHash = Animator.StringToHash(name);

        if (animator == null || !animator.HasState(layer, stateHash))
        {
            Debug.LogWarning($"SaveIconView: state '{name}' not found on layer {layer}", this);
            stateHash = 0;
            return;
        }

        if (fade)
            animator.CrossFade(stateHash, crossFade, layer);
        else
            animator.Play(stateHash, layer, 0f);
    }

    bool Finished()
    {
        if (stateHash == 0 || timer <= 0f)
            return true;
        if (animator.IsInTransition(layer))
            return false;

        var info = animator.GetCurrentAnimatorStateInfo(layer);
        return info.shortNameHash == stateHash && info.normalizedTime >= 1f;
    }

    void SetVisible(bool visible,bool immediate = false)
    {
        if (group == null)
            return;
        if (immediate)
        {
            group.alpha = visible ? 1f : 0f;
            return;
        }
        group.DOKill();
        group.DOFade(visible ? 1f : 0f, 0.2f).SetUpdate(unscaledTime);
    }
}
