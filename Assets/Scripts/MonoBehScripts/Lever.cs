using System;
using UnityEngine;
using Sirenix.OdinInspector;

public class Lever : BoolStateObject
{
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    private readonly int UseHash = Animator.StringToHash("Use");
    private readonly int UsedHash = Animator.StringToHash("Used");
    [SerializeField] private AudioSource LeverStartSound;

    [Header("Events")]
    [SerializeField] private BetterEvent _onUse;
    public BetterEvent OnStartAfterSave;

    protected override void OnLoaded()
    {
        _animator.Play(UsedHash, 0, 1f);
        OnStartAfterSave.Invoke();
    }

    [Button("TRIGGER", ButtonSizes.Small, ButtonStyle.Box)]
    public void Use()
    {
        if (IsUsed) return;

        Save(true);
        _animator.Play(UseHash);
        LeverStartSound.Play();
        _onUse.Invoke();
    }
}