using Controllers;
using UnityEngine;

namespace Systems
{
    public interface ISystem
    {
        public void Initialize(Controller owner);

        public void OnUpdate();
    }
    
    public interface IComponent
    {
    }

}