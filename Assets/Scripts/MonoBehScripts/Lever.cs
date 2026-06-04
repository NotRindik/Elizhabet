using System;
using UnityEngine;
using Sirenix.OdinInspector;

public class Lever : SerializedMonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    private readonly int UseHash = Animator.StringToHash("Use");
    private readonly int UsedHash = Animator.StringToHash("Used");
    [SerializeField]private AudioSource LeverStartSound;

    [Header("Events")]
    [SerializeField] private BetterEvent _onUse;

    [Header("Save")]
    [SerializeField] private string _localKey = "used";

    private bool _isUsed;
    public BetterEvent OnStartAfterSave;
    
    private WorldObjectsStateSave WorldSave =>
        SaveManager.Instance.GetModule<WorldObjectsStateSave>();

    private string SaveKey => WorldKeyBuilder.Build(this, _localKey);

    private void Start()
    {
        if (WorldSave.Exist(SaveKey))
        {
            _isUsed = WorldSave.GetData(SaveKey) == "1";
            _animator.Play(UsedHash, 0, 1f);
            OnStartAfterSave.Invoke();
        }
    }
    
    [Button("TRIGGER", ButtonSizes.Small, ButtonStyle.Box)]
    public void Use()
    {
        if(_isUsed)
            return;
        
        _isUsed = true;
        WorldSave.SetData(SaveKey, "1");
        SaveManager.Instance.SaveModule<WorldObjectsStateSave>();

        _animator.Play(UseHash);
        LeverStartSound.Play();
        _onUse.Invoke();
    }
}