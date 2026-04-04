using UnityEngine;
using DG.Tweening;
using System.Collections;
using Sirenix.OdinInspector.Editor;
using Controllers;

public class LevitationPlatform : MonoBehaviour
{
    [System.Serializable]
    public class PathPoint
    {
        public Transform point;
        public float duration = 1f;
        public Ease ease = Ease.Linear;
    }

    public PathPoint[] path;
    public bool loop = true;

    private Sequence sequence;

    public BetterEvent OnEnd;

    private Vector3 _lastPosition;
    public Vector2 DeltaVelocity { get; private set; }

    private void Start()
    {
        _lastPosition = transform.position;
    }

    public void OnCollisionStay2D(Collision2D collision)
    {
        if(collision.gameObject.TryGetComponent<AbstractEntity>(out var abstractEntity))
        {
            abstractEntity.GetControllerComponent<ControllersBaseFields>().rb.position += DeltaVelocity * Time.deltaTime;
        }
    }
    private void LateUpdate()
    {
        Vector3 currentPosition = transform.position;

        DeltaVelocity = (currentPosition - _lastPosition) / Time.deltaTime;

        _lastPosition = currentPosition;
    }


    public void StartMove()
    {
        BuildSequence();
    }

    private void BuildSequence()
    {
        if (path == null || path.Length < 2)
            return;

        sequence = DOTween.Sequence();

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 targetPos = path[i].point.position;
            float duration = path[i].duration;
            Ease ease = path[i].ease;

            sequence.Append(
                transform.DOMove(targetPos, duration)
                         .SetEase(ease)
            );
        }

        sequence.OnComplete(()=> OnEnd.Invoke());

        if (loop)
            sequence.SetLoops(-1, LoopType.Restart);
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}