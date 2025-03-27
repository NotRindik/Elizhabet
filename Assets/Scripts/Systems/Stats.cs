using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Systems
{
    public class Stats : IComponent
    {
        private Dictionary<string, object> _stats = new Dictionary<string, object>();
        public Action OnDataChange;

        public object this[string key]
        {
            get
            {
                return _stats[key];
            }
            set
            {
                _stats[key] = value;
                OnDataChange?.Invoke();
            }
        }
    }
}