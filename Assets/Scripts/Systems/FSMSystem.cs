using System;
using System.Collections.Generic;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{
    public class FSMSystem : BaseSystem,IDisposable
    {
        private IState currentState;
        private Dictionary<IState,List<Transition>> transitions = new();
        private List<Transition> anyTransitions = new();
        private FsmComponent _fsmComponent;
        public override void Initialize(AbstractEntity owner)
        {
            _fsmComponent = owner.GetControllerComponent<FsmComponent>();
            base.Initialize(owner);
            owner.OnUpdate += Update;
            owner.OnFixedUpdate += OnFixedUpdate;
            owner.OnLateUpdate += OnLateUpdate;
        }

        public void SetState(IState newState)
        {
            if (newState == currentState) return;
            _fsmComponent.currentState = newState.ToString();
            _fsmComponent.state = newState;
            currentState?.Exit();
            currentState = newState;
            currentState.Enter();
        }


        public void AddTransition(IState from, IState to, Func<bool> condition)
        {
            var transition = new Transition(from, to, condition);
            if (!transitions.TryGetValue(from, out var list))
            {
                list = new List<Transition>();
                transitions[from] = list;
            }
            list.Add(transition);
        }
        public void AddAnyTransition(IState to, Func<bool> condition)
        {
            anyTransitions.Add(new Transition(null, to, condition));
        }

        public override void OnUpdate()
        {
            var transition = GetTransition();
            if (transition != null)
            {
                SetState(transition.To);
            }

            currentState?.Update();
        }
        public virtual void OnFixedUpdate()
        {
            if (!IsActive)
            {
                return;
            }

            currentState?.FixedUpdate();
        }
        public virtual void OnLateUpdate()
        {
            if (!IsActive)
            {
                return;
            }

            currentState?.LateUpdate();
        }

        private Transition GetTransition()
        {
            if (currentState != null)
            {
                if (transitions.TryGetValue(currentState, out var list))
                {
                    foreach (var t in list)
                    {
                        if (t.Condition()) return t;
                    }
                }
            }

            
            foreach (var t in anyTransitions)
            {
                if (t.Condition()) return t;
            }
            
            return null;
        }

        public void Dispose()
        {
            owner.OnUpdate -= Update;
            owner.OnFixedUpdate -= OnFixedUpdate;
            owner.OnLateUpdate -= OnLateUpdate;
        }
    }

    public class Transition
    {
        public IState From;
        public IState To;
        public Func<bool> Condition;

        public Transition(IState from, IState to, Func<bool> condition)
        {
            From = from;
            To = to;
            Condition = condition;
        }
    }
    
    [System.Serializable]
    public class FsmComponent : IComponent
    {
        public string currentState;
        public IState state;
    }
}

namespace States
{
    public interface IState
    {
        void Enter();
        public void Update()
        {
            
        }
        public void LateUpdate()
        {
            
        }
        public void FixedUpdate()
        {
            
        }
        void Exit();
    }
}