using System;
using System.Collections.Generic;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{
    public class FSMSystem : BaseSystem
    {
        private IState currentState;
        private List<Transition> transitions = new();
        private List<Transition> fixedTransitions = new();
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
            currentState?.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public void AddTransition(IState from, IState to, Func<bool> condition)
        {
            transitions.Add(new Transition(from, to, condition));
        }
        public void AddFixedTransition(IState from, IState to, Func<bool> condition)
        {
            fixedTransitions.Add(new Transition(from, to, condition));
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
            var transition = GetFixedTransition();
            if (transition != null)
            {
                SetState(transition.To);
            }
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
            foreach (var t in transitions)
                if (t.From == currentState && t.Condition())
                {
                    return t;
                }
            
            foreach (var t in anyTransitions)
                if (t.Condition())
                {
                    return t;
                }
            return null;
        }
        
        private Transition GetFixedTransition()
        {
            foreach (var t in fixedTransitions)
                if (t.From == currentState && t.Condition())
                {
                    return t;
                }
            return null;
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
    }
}

namespace States
{
    public interface IState
    {
        void Enter();
        void Update()
        {
            
        }
        void LateUpdate()
        {
            
        }
        void FixedUpdate()
        {
            
        }
        void Exit();
    }
}