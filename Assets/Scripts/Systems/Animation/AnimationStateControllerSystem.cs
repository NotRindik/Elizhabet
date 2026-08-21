using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

            if(_composer != null)
            {
                _animationList = _composer.animations.Values.ToArray();
            }
            else
            {
                _animationList = new AnimationComponent[1] { owner.GetControllerComponent<AnimationComponent>() };
            }
        }
        

        public override void OnUpdate()
        {
            base.OnUpdate();

            foreach (var composer in _animationList)
            {
                if(composer.events == null || composer.events?.Length == 0)
                    continue;

                var state = composer.animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName(composer.currentState))
                    continue;

                float t = state.normalizedTime % 1f;

                bool looped = t < composer.previousNormalizedTime && state.loop;
                bool stateChanged = state.fullPathHash != composer.previousStateHash;

                if (looped /*|| stateChanged */)
                {
                    for (int i = 0; i < composer.events.Length; i++)
                    {
                        var e = composer.events[i];
                        e.fired = false;
                        composer.events[i] = e;
                    }
                }

                composer.previousNormalizedTime = t;
                composer.previousStateHash = state.fullPathHash;

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
        [HideInInspector] public float previousNormalizedTime;
        [HideInInspector] public int previousStateHash;

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
        
        public void SetAllFired()
        {
            for (int i = 0; i < events.Length; i++)
            {
                events[i].fired = true;
            }

            previousStateHash = Animator.StringToHash(currentState);
        }
        
        public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
            currentState = stateName;
            ResetEvents();
            animator.Play(stateName, layer, normalizedTime);

            animator.Update(0f);
            OnAnimationStateChange?.Invoke(stateName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetProgress( int layer = 0)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layer);

            if (!info.IsName(currentState))
                return 0f;

            return info.normalizedTime % 1f;;
        }
        
        public float GetProgressRaw(int layer = 0)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (!info.IsName(currentState))
                return 0f;

            return info.normalizedTime;
        }

        public bool IsTransitioning(int layer = 0)
        {
            return animator.IsInTransition(layer);
        }
    }

    public class AnimationComposerSystem : BaseSystem,IDisposable
    {
        private AnimationComponentsComposer _animationComponentsComposer;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            
            _animationComponentsComposer = owner.GetControllerComponent<AnimationComponentsComposer>();
        }
        
        public void Dispose()
        {
        }
    }

     [Serializable]
    public class AnimationComponentsComposer : IComponent
    {
        public SerializedDictionary<string, AnimationComponent> animations;

        [SerializeField]
        public AnimationComposerConfig config;

        public string CurrentState { get; private set; }

        public event Action<string> OnAnimationStateChange;

        private readonly HashSet<string> _lockedParts = new();

        public void LockPart(string partName)
        {
            _lockedParts.Add(partName);
        }

        public void UnlockPart(string partName)
        {
            _lockedParts.Remove(partName);
        }

        public void UnlockAll()
        {
            _lockedParts.Clear();
        }

        public AnimationComponentsComposer LockParts(params string[] partNames)
        {
            foreach (var part in partNames)
                _lockedParts.Add(part);

            return this;
        }

        public AnimationComponentsComposer UnlockParts(params string[] partNames)
        {
            foreach (var part in partNames)
                _lockedParts.Remove(part);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLocked(string partName)
        {
            return _lockedParts.Contains(partName);
        }

        private AnimationStateConfig GetState(string stateName)
        {
            if (config == null || config.states == null)
                return null;

            for (int i = 0; i < config.states.Count; i++)
            {
                var state = config.states[i];

                if (state != null && state.stateName == stateName)
                    return state;
            }

            return null;
        }

        public float GetLockedProgressOfStateRaw(
            string stateName,
            int layer = 0)
        {
            var state = GetState(stateName);

            if (state == null)
                return 0f;

            float minProgress = float.MaxValue;
            bool any = false;

            foreach (var part in state.parts)
            {
                if (!IsLocked(part.partName))
                    continue;

                if (part.clip == null)
                    continue;

                if (!animations.TryGetValue(part.partName, out var anim))
                    continue;

                if (anim.currentState != part.AnimatorStateAlias)
                    continue;

                any = true;

                float progress = anim.GetProgressRaw(layer);

                if (progress < minProgress)
                    minProgress = progress;
            }

            return any ? minProgress : 0f;
        }

        public float GetStateProgress(int layer = 0)
        {
            var state = GetState(CurrentState);

            if (state == null)
                return 0f;

            float total = 0f;
            int count = 0;

            foreach (var part in state.parts)
            {
                if (IsLocked(part.partName))
                    continue;

                if (part.clip == null)
                    continue;

                if (!animations.TryGetValue(part.partName, out var anim))
                    continue;

                if (anim.currentState != part.AnimatorStateAlias)
                    continue;

                total += anim.GetProgress(layer);
                count++;
            }

            return count > 0
                ? total / count
                : 0f;
        }

        public float GetLockedProgressOfState(
            string stateName,
            int layer = 0)
        {
            var state = GetState(stateName);

            if (state == null)
            {
                Debug.LogWarning(
                    $"[Progress] Нет состояния '{stateName}' в config!");
                return 0f;
            }

            float maxProgress = 0f;

            foreach (var part in state.parts)
            {
                if (!IsLocked(part.partName))
                    continue;

                if (part.clip == null)
                    continue;

                if (!animations.TryGetValue(part.partName, out var anim))
                    continue;

                if (anim.currentState != part.AnimatorStateAlias)
                    continue;

                float progress = anim.GetProgress(layer);

                if (progress > maxProgress)
                    maxProgress = progress;
            }

            return maxProgress;
        }

        public void PlayState(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
            var state = GetState(stateName);

            if (state == null)
                return;

            CurrentState = state.stateName;

            foreach (var part in state.parts)
            {
                if (IsLocked(part.partName))
                    continue;

                if (part.clip == null)
                    continue;

                if (animations.TryGetValue(part.partName, out var anim))
                {
                    anim.Play(part.AnimatorStateAlias, layer, normalizedTime);
                }
            }

            OnAnimationStateChange?.Invoke(stateName);
        }

        public void CrossFadeState( string stateName, float duration)
        {
            var state = GetState(stateName);

            if (state == null)
                return;

            CurrentState = state.stateName;

            foreach (var part in state.parts)
            {
                if (IsLocked(part.partName))
                    continue;

                if (part.clip == null)
                    continue;
                
                if (animations.TryGetValue(part.partName, out var anim))
                {
                    anim.CrossFade( part.AnimatorStateAlias, duration);
                }
            }

            OnAnimationStateChange?.Invoke(stateName);
        }

        public void PlayOnPart(
            string partName,
            string stateName,
            int layer = -1,
            float normalizedTime = float.NegativeInfinity)
        {
            if (IsLocked(partName))
                return;

            if (animations.TryGetValue(partName, out var anim))
            {
                anim.Play(
                    stateName,
                    layer,
                    normalizedTime);
            }
        }

        public void SetSpeedOfPart(
            string part,
            float speed)
        {
            if (animations.TryGetValue(part, out var anim))
                anim.SetAnimationSpeed(speed);
        }

        public void SetSpeedOfParts(
            float speed,
            params string[] parts)
        {
            foreach (var part in parts)
            {
                if (animations.TryGetValue(part, out var anim))
                    anim.SetAnimationSpeed(speed);
            }
        }

        public AnimationComponentsComposer StopPlaybackOfParts(
            params string[] parts)
        {
            foreach (var part in parts)
            {
                if (animations.TryGetValue(part, out var anim))
                    anim.animator.enabled = false;
            }

            return this;
        }

        public AnimationComponentsComposer StartPlaybackOfParts(
            params string[] parts)
        {
            foreach (var part in parts)
            {
                if (animations.TryGetValue(part, out var anim))
                    anim.animator.enabled = true;
            }

            return this;
        }

        public void SetSpeedAll(float speed)
        {
            foreach (var anim in animations.Values)
                anim.SetAnimationSpeed(speed);
        }
    }
}