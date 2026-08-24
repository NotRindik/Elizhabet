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
            // ШАГ 1 TODO: у CrossFade уже есть guard "if (currentState == name) return;",
            // а тут его нет — если резолвер (см. ниже) дёрнет Play с уже играющим состоянием,
            // анимация перезапустится с нулевого кадра. Резолвер сам подстраховывается сверху
            // (сравнивает currentState перед вызовом Play), но сюда тоже стоит добавить —
            // сделаю в следующем шаге вместе с чисткой PlayOnPart.
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
    
    public class AnimationLayer
    {
        public readonly string Name;
        public readonly HashSet<string> Mask = new(); // какие части этот слой вообще вправе трогать
        public AnimationStateConfig CurrentStateConfig;

        // Обычные слои (Locomotion/Action/Reaction): CurrentStateConfig == null
        // значит "слой сейчас просто ничем не занят" — резолвер должен смотреть
        // ниже, а не молчать. Override — противоположный случай: у него
        // ПРИНЦИПИАЛЬНО никогда нет CurrentStateConfig (TakeControl только
        // трогает маску), но именно отсутствие анимации там и есть его смысл —
        // часть должна замолчать, а не провалиться вниз к Action/Locomotion.
        public bool BlocksWhenInactive;

        public AnimationLayer(string name)
        {
            Name = name;
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
        

        private readonly List<AnimationLayer> _layers = new(); // порядок = приоритет, последний — самый сильный
        private readonly Dictionary<string, AnimationLayer> _layersByName = new();

        private const string OverrideLayerName = "Override";

        // Раскладывает рантайм-слои строго в порядке из ассета — один раз.
        // ВАЖНО: раньше порядок _layers определялся тем, в каком порядке
        // геймплейный код первый раз вызвал PlayState на каждый слой — то есть
        // приоритет случайно зависел от того, что игрок сделал раньше. Теперь,
        // когда порядок списка И ЕСТЬ приоритет, так нельзя: планировка должна
        // прийти из конфига целиком и сразу, до первого обращения.
        private void EnsureLayersInitialized()
        {
            if (_layers.Count > 0 || config == null || config.layers == null)
                return;

            foreach (var layerCfg in config.layers)
            {
                if (layerCfg == null || string.IsNullOrEmpty(layerCfg.layerName))
                    continue;

                var layer = new AnimationLayer(layerCfg.layerName);
                if (layerCfg.maskParts != null)
                    foreach (var part in layerCfg.maskParts)
                        layer.Mask.Add(part);

                _layersByName[layerCfg.layerName] = layer;
                _layers.Add(layer);
            }
        }

        // "Override" — эксклюзивный контроль (оружие и т.п.). В ассете его
        // заводить не обязательно: если его там нет, композер создаёт его сам
        // как САМЫЙ СИЛЬНЫЙ (кладёт последним), при первом TakeControl.
        private AnimationLayer GetOrCreateOverrideLayer()
        {
            EnsureLayersInitialized();

            if (!_layersByName.TryGetValue(OverrideLayerName, out var layer))
            {
                layer = new AnimationLayer(OverrideLayerName) { BlocksWhenInactive = true };
                _layersByName[OverrideLayerName] = layer;
                _layers.Add(layer);
            }

            return layer;
        }

        public void ClearLayer(string layerName)
        {
            EnsureLayersInitialized();
            if (!_layersByName.TryGetValue(layerName, out var layer))
                return;

            layer.CurrentStateConfig = null;
            ResolveAllTouchedParts();
        }

        // Имя текущего состояния КОНКРЕТНОГО слоя — замена глобальному CurrentState
        // для мест вида "if (CurrentState != X) CrossFadeState(...)", которые раньше
        // работали, пока слой был один. null, если слой ещё не трогали.
        public string GetLayerState(string layerName)
        {
            EnsureLayersInitialized();
            return _layersByName.TryGetValue(layerName, out var layer)
                ? layer.CurrentStateConfig?.stateName
                : null;
        }

        // Замена GetLockedProgressOfStateRaw — та же семантика (минимальный raw-прогресс
        // среди частей), но скоуп по слою вместо _lockedParts.
        public float GetLayerStateProgressRaw(string layerName, string stateName, int animatorLayer = 0)
        {
            EnsureLayersInitialized();
            if (!_layersByName.TryGetValue(layerName, out var layer))
                return 0f;

            var cfg = layer.CurrentStateConfig;
            if (cfg == null || cfg.stateName != stateName)
                return 0f;

            float minProgress = float.MaxValue;
            bool any = false;

            foreach (var part in cfg.parts)
            {
                if (part.clip == null) continue;
                if (!animations.TryGetValue(part.partName, out var anim)) continue;
                if (anim.currentState != part.AnimatorStateAlias) continue;

                any = true;
                float progress = anim.GetProgressRaw(animatorLayer);
                if (progress < minProgress) minProgress = progress;
            }

            return any ? minProgress : 0f;
        }

        // TakeControl/ReleaseControl теперь просто добавляют/убирают часть из
        // маски служебного слоя Override — того же самого механизма, который
        // используется для обычной композиции, а не отдельной системы поверх.
        public void TakeControl(string partName)
        {
            GetOrCreateOverrideLayer().Mask.Add(partName);
            ResolvePart(partName);
        }

        public void ReleaseControl(string partName)
        {
            if (_layersByName.TryGetValue(OverrideLayerName, out var layer))
                layer.Mask.Remove(partName);
            ResolvePart(partName);
        }

        public void TakeControl(params string[] partNames)
        {
            var layer = GetOrCreateOverrideLayer();
            foreach (var p in partNames)
                layer.Mask.Add(p);

            foreach (var p in partNames)
                ResolvePart(p);
        }

        public void ReleaseControl(params string[] partNames)
        {
            if (_layersByName.TryGetValue(OverrideLayerName, out var layer))
                foreach (var p in partNames)
                    layer.Mask.Remove(p);

            foreach (var p in partNames)
                ResolvePart(p);
        }

        // Порядок = приоритет: последний в списке, чья МАСКА содержит эту
        // часть, побеждает — независимо от того, есть ли у него анимация на
        // эту часть в текущем присвоенном состоянии. Если маску выиграл, а
        // анимации для части нет — часть НЕ анимируется вообще (провала вниз,
        // к следующему слою, нет). Это и даёт эксклюзивность бесплатно: слой
        // с частью в маске, но без активного состояния (Override), просто
        // затыкает часть — её отдаёт внешний код (IK, ручная поза и т.п.).
        private bool TryGetWinner(string partName, out string winningAlias)
        {
            winningAlias = null;
            EnsureLayersInitialized();

            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                var layer = _layers[i];
                if (!layer.Mask.Contains(partName))
                    continue;

                var cfg = layer.CurrentStateConfig;
                if (cfg == null)
                {
                    // Слой просто сейчас ничем не занят (Reaction без активного
                    // TakeHit, Action между атаками) — не мешаем нижним слоям.
                    // Override — исключение: у него cfg всегда null по
                    // конструкции, но его смысл именно в том, чтобы молчать.
                    if (layer.BlocksWhenInactive)
                        return false;
                    continue;
                }

                foreach (var part in cfg.parts)
                {
                    if (part.partName == partName && part.clip != null)
                    {
                        winningAlias = part.AnimatorStateAlias;
                        return true;
                    }
                }

                return false; // слой активен, но именно эту часть сейчас не анимирует — тишина, не провал
            }

            return false; // ни один слой не претендует на эту часть маской вообще
        }

        private void ResolvePart(string partName, int animatorLayer = -1, float normalizedTime = float.NegativeInfinity)
        {
            if (!TryGetWinner(partName, out var winningAlias)) return;
            if (!animations.TryGetValue(partName, out var anim)) return;
            
            if (anim.currentState != winningAlias)
                anim.Play(winningAlias, animatorLayer, normalizedTime);
        }

        private void ResolvePartCrossFade(string partName, float duration)
        {
            if (!TryGetWinner(partName, out var winningAlias)) return;
            if (!animations.TryGetValue(partName, out var anim)) return;

            anim.CrossFade(winningAlias, duration); 
        }

        // Раньше собирала "затронутые части" из cfg.parts всех слоёв — но теперь
        // композиция управляется МАСКОЙ, а не тем, что конкретно сейчас играет.
        // Часть может нуждаться в пересчёте просто потому, что она в чьей-то
        // маске (например, Override освободил её) — даже если ни у одного слоя
        // сейчас нет активного состояния с этой частью.
        private void ResolveAllTouchedParts()
        {
            EnsureLayersInitialized();
            var touched = new HashSet<string>();

            foreach (var layer in _layers)
                touched.UnionWith(layer.Mask);

            foreach (var partName in touched)
                ResolvePart(partName);
        }
        
        private AnimationStateConfig GetState(string layerName, string stateName)
        {
            if (config == null || config.layers == null)
                return null;

            AnimationLayerConfig layerCfg = null;
            for (int i = 0; i < config.layers.Count; i++)
            {
                if (config.layers[i] != null && config.layers[i].layerName == layerName)
                {
                    layerCfg = config.layers[i];
                    break;
                }
            }

            if (layerCfg == null || layerCfg.states == null)
                return null;

            for (int i = 0; i < layerCfg.states.Count; i++)
            {
                var state = layerCfg.states[i];
                if (state != null && state.stateName == stateName)
                    return state;
            }

            return null;
        }
        
        public void PlayState(string layerName, string stateName, int animatorLayer = -1, float normalizedTime = float.NegativeInfinity)
        {
            EnsureLayersInitialized();
            var state = GetState(layerName, stateName);
            if (state == null) return;

            if (!_layersByName.TryGetValue(layerName, out var targetLayer))
            {
                Debug.LogWarning($"[AnimationComponentsComposer] Слой '{layerName}' не найден в конфиге — PlayState('{stateName}') проигнорирован.");
                return;
            }

            targetLayer.CurrentStateConfig = state;
            CurrentState = stateName;
            
            foreach (var partName in targetLayer.Mask)
                ResolvePart(partName, animatorLayer, normalizedTime);

            OnAnimationStateChange?.Invoke(stateName);
        }

        public void CrossFadeState(string layerName, string stateName, float duration)
        {
            EnsureLayersInitialized();
            var state = GetState(layerName, stateName);
            if (state == null) return;

            if (!_layersByName.TryGetValue(layerName, out var targetLayer))
            {
                Debug.LogWarning($"[AnimationComponentsComposer] Слой '{layerName}' не найден в конфиге — CrossFadeState('{stateName}') проигнорирован.");
                return;
            }

            targetLayer.CurrentStateConfig = state;
            CurrentState = stateName;

            foreach (var partName in targetLayer.Mask)
                ResolvePartCrossFade(partName, duration);

            OnAnimationStateChange?.Invoke(stateName);
        }
        
        public void PlayOnPart( string partName, string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
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