using UnityEngine;

namespace Systems
{
    public interface ISystem
    {
        public void Initialize(GameObject owner)
        {
        
        }

        public void Update()
        {
        
        }
    }
    
    public interface IComponent
    {
    }
}