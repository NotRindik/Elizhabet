using System;
using UnityEngine;

public class Door : MonoBehaviour
{
    private int _openHash = Animator.StringToHash("open");
    private int _closeHash = Animator.StringToHash("close");

    [SerializeField] private Animator _animator;

    public AudioClip openSound;
    public AudioClip closeSound;
    

    public void Open(bool immediate)
    {
        _animator.Play(_openHash,0,immediate ? 1 : 0);
        if(!immediate)
            AudioManager.instance.PlaySoundEffect(openSound);
    }

    public void Close(bool immediate)
    {
        _animator.Play(_closeHash,0,immediate ? 1 : 0);
        if(!immediate)
            AudioManager.instance.PlaySoundEffect(closeSound);
    }
}
