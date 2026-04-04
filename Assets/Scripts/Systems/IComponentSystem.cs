using Controllers;
using UnityEngine;

namespace Systems
{
    public interface ISystem
    {
        public void Initialize(AbstractEntity owner);

        public void OnUpdate();
    }
    
    public interface IComponent
    {
    }

}