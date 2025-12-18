using Controllers;
using UnityEngine;

namespace Systems
{
    public interface ISystem
    {
        public void Initialize(IController owner);

        public void OnUpdate();
    }
    
    public interface IComponent
    {
    }

}