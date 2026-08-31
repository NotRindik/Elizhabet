using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class AnimationSystem : SerializedMonoBehaviour
{
    public SpriteRenderer target;

    public AnimationData[] animations;

    private AnimationData _current;
    private int _frame;
    private float _timer;

    private bool stateEnd = false;

    public Action onStateEnd; 
    

    public void Play(string state)
    {
        if (_current.name == state)
            return;

        for (int i = 0; i < animations.Length; i++)
        {
            if (animations[i].name != state)
                continue;

            _current = animations[i];
            _frame = 0;
            _timer = 0f;
            stateEnd = false;

            if (_current.frames == null ||
                _current.frames.Count == 0)
                return;

            ApplyFrame();

            return;
        }
    }
    
    public void Play(string state,bool immediate)
    {
        if (_current.name == state)
            return;

        for (int i = 0; i < animations.Length; i++)
        {
            if (animations[i].name != state)
                continue;

            _current = animations[i];
            _frame = _current.frames.Count - 1;
            _timer = 0f;
            stateEnd = false;

            if (_current.frames == null ||
                _current.frames.Count == 0)
                return;

            ApplyFrame(immediate);

            return;
        }
    }

    private void Update()
    {
        if (_current.frames == null || _current.frames.Count == 0)
            return;

        if (_current.fps <= 0f)
            return;

        _timer += Time.deltaTime;

        float frameTime = 1f / _current.fps;

        if (_timer >= frameTime)
        {
            _timer -= frameTime;

            _frame++;

            if (_frame >= _current.frames.Count)
            {
                if (_current.loop)
                    _frame = 0;
                else
                {
                    if(stateEnd)
                        return;
                    _frame = _current.frames.Count - 1;
                    onStateEnd?.Invoke();
                    stateEnd = true;
                }
            }

            ApplyFrame();
        }
    }

    private void ApplyFrame(bool ignoreEvents = false)
    {
        if(!ignoreEvents) 
            _current.frames[_frame].animationEvent?.Invoke();
        
        if(_current.frames[_frame].frame == null)
            return;
        
        target.sprite = _current.frames[_frame].frame;
    }

    [Serializable]
    public struct AnimationData
    {
        public string name;

        [ListDrawerSettings(
            ShowIndexLabels = true,
            DraggableItems = true,
            Expanded = true
        )]
        public List<AnimationKey> frames;

        public float fps;
        public bool loop;
    }

    [Serializable]
    [InlineProperty]
    public struct AnimationKey
    {
        [PreviewField(100, ObjectFieldAlignment.Left)]
        [HideLabel]
        public Sprite frame;

        [HideLabel]
        public UnityEvent animationEvent;
    }
}