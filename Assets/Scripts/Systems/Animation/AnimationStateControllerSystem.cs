using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Systems
{
    [System.Serializable]
    public struct AnimationEvent
    {
        public string name;

        [Range(0f, 1f)]
        public float normalizedTime;

        public UnityEvent onEvent;

        public bool fired;
    }


    public class AnimationEventsUpdater : BaseSystem, IDisposable
    {
        private AnimationComponentsComposer _composer;

        private  AnimationComponent[] _animationList;

        private string currstate;

        public void Dispose()
        {
            owner.OnLateUpdate -= Update;
        }

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);

            _composer = owner.GetControllerComponent<AnimationComponentsComposer>();

            owner.OnLateUpdate += Update;

            _animationList = _animationList = _composer.animations.Values.ToArray();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            foreach (var composer in _animationList)
            {
                if(composer.events == null || composer.events?.Length == 0)
                    continue;

                var state = composer.animator.GetCurrentAnimatorStateInfo(0);

                for (int i = 0; i < composer.events.Length; i++)
                {
                    if(composer.currentState != composer.events[i].name)
                    {
                        continue;
                    }
                    if (composer.events[i].fired)
                    {
                        continue;
                    }

                    float t = state.normalizedTime % 1f;

                    if (t >= composer.events[i].normalizedTime)
                    {
                        composer.events[i].onEvent?.Invoke();
                        composer.events[i].fired = true;
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class AnimationComponent : IComponent
    {

        public string currentState;

        public Animator animator;
        public Action<string> OnAnimationStateChange;

        public AnimationEvent[] events;

        public void SetAnimationSpeed(float speed)
        {
            animator.speed = speed;
        }

        public void CrossFade(string name, float delta)
        {
            if (currentState == name)
                return;
            currentState = name;
            ResetEvents();
            animator.CrossFade(name, delta, 0);
            OnAnimationStateChange?.Invoke(name);
        }
        public void ResetEvents()
        {
            for (int i = 0; i < events.Length; i++)
            {
                events[i].fired = false;
            }
        }
        public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
            currentState = stateName;
            ResetEvents();
            animator.Play(stateName, layer, normalizedTime);

            animator.Update(0f);
            OnAnimationStateChange?.Invoke(stateName);
        }
    }

    public class AnimationComposerSystem : BaseSystem
    {
        private AnimationComponentsComposer _animationComponentsComposer;
        public override void Initialize(AbstractEntity owner)
        {
            _animationComponentsComposer = owner.GetControllerComponent<AnimationComponentsComposer>();

        }
    }

    [System.Serializable]
    public class AnimationComponentsComposer : IComponent
    {
        public SerializedDictionary<string, AnimationComponent> animations;
        public Dictionary<string, AnimationState> states = new();

        public string CurrentState { get; private set; }
        public event Action<string> OnAnimationStateChange;

        private HashSet<string> _lockedParts = new();

        public void LockPart(string partName) => _lockedParts.Add(partName);
        public void UnlockPart(string partName) => _lockedParts.Remove(partName);
        public void UnlockAll() => _lockedParts.Clear();


        public AnimationComponentsComposer LockParts(params string[] partName)
        { 
            foreach (var part in partName)
                _lockedParts.Add(part);
            return this;
        }
        public AnimationComponentsComposer UnlockParts(params string[] partName)
        {
            foreach (var item in partName)
            {
                _lockedParts.Remove(item);
            }
            return this;
        }
        private bool IsLocked(string partName) => _lockedParts.Contains(partName);

        public void AddState(string stateName, Action<AnimationState.AnimationStateBuilder> buildAction)
        {
            var builder = new AnimationState.AnimationStateBuilder(stateName);
            buildAction(builder);
            states[stateName] = builder.Build();
        }

        public void PlayState(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
            if (!states.TryGetValue(stateName, out var state))
                return;

            CurrentState = state.Name;

            foreach (var part in state.Parts)
            {
                if (IsLocked(part.Key))
                    continue;

                if (animations.TryGetValue(part.Key, out var anim))
                    anim.Play(part.Value, layer, normalizedTime);
            }

            OnAnimationStateChange?.Invoke(stateName);
        }

        public void SetSpeedOfPart(string part,float speed)
        {
            animations[part].SetAnimationSpeed(speed);
        }

        public void SetSpeedOfParts(float speed, params string[] part)
        {
            foreach (var item in part)
            {
                animations[item].SetAnimationSpeed(speed);
            }
        }
        public AnimationComponentsComposer StopPlaybackOfParts(params string[] part)
        {
            foreach (var item in part)
            {
                animations[item].animator.enabled = false;
            }
            return this;
        }
        public AnimationComponentsComposer StartPlaybackOfParts(params string[] part)
        {
            foreach (var item in part)
            {
                animations[item].animator.enabled = true;
            }
            return this;
        }
        public void SetSpeedAll(float speed)
        {
            foreach (var item in animations.Values)
            {
                item.SetAnimationSpeed(speed);
            }
        }

        public void CrossFadeState(string stateName, float duration)
        {
            if (!states.TryGetValue(stateName, out var state))
                return;

            CurrentState = state.Name;

            foreach (var part in state.Parts)
            {
                if (IsLocked(part.Key)) // 🔒 пропускаем
                    continue;

                if (animations.TryGetValue(part.Key, out var anim))
                    anim.CrossFade(part.Value, duration);
            }

            OnAnimationStateChange?.Invoke(stateName);
        }



        public void PlayOnPart(string partName, string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
            if (IsLocked(partName)) // 🔒 если заблокирован — ничего
                return;

            if (animations.TryGetValue(partName, out var anim))
                anim.Play(stateName, layer, normalizedTime);
        }
    }
}

public class AnimationState
{
    public string Name { get; }
    public Dictionary<string, string> Parts { get; }

    public AnimationState(string name, Dictionary<string, string> parts)
    {
        Name = name;
        Parts = parts;
    }

    public class AnimationStateBuilder
    {
        private readonly string _name;
        private readonly Dictionary<string, string> _parts = new();

        public AnimationStateBuilder(string name) => _name = name;

        public AnimationStateBuilder Part(string bodyPart, string animName)
        {
            _parts[bodyPart] = animName;
            return this;
        }

        public AnimationState Build() => new AnimationState(_name, _parts);
    }
}