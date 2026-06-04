using System;
using UnityEngine;

public class Door : MonoBehaviour
{
    private int _openHash = Animator.StringToHash("open");
    private int _closeHash = Animator.StringToHash("close");

    private Animator _animator;

    private void Start()
    {
        _animator = GetComponent<Animator>();
    }

    public void Open(bool immediate)
    {
        _animator.Play(_openHash,0,immediate ? 1 : 0);
    }

    public void Close(bool immediate)
    {
        _animator.Play(_closeHash,0,immediate ? 1 : 0);
    }
}
