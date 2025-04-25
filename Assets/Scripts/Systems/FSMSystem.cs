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
        private List<Transition> anyTransitions = new();
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            owner.OnUpdate += Update;
        }

        public void SetState(IState newState)
        {
            if (newState == currentState) return;

            currentState?.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public void AddTransition(IState from, IState to, Func<bool> condition)
        {
            transitions.Add(new Transition(from, to, condition));
        }

        public void AddAnyTransition(IState to, Func<bool> condition)
        {
            anyTransitions.Add(new Transition(null, to, condition));
        }

        public override void Update()
        {
            var transition = GetTransition();
            if (transition != null)
                SetState(transition.To);

            currentState?.Update();
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
                    return t;
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
}

namespace States
{
    public interface IState
    {
        void Enter();
        void Update();
        void Exit();
    }
}