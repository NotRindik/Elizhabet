using UnityEngine;
namespace Systems
{
    public class MaterialSystem : BaseSystem
    {
        public MaterialComponent materialComponent;

        public override void Initialize(AbstractEntity owner)
        {   
            base.Initialize(owner);
            materialComponent = owner.GetControllerComponent<MaterialComponent>();
        }

        public void AddTexture(int index,Sprite sprite)
        {
            string propertyName = index switch
            {
                0 => "_LUT",
                _ => $"_LUT{index}"
            };
            for (int i = 0; i < materialComponent.materials.Length; i++)
            {
                materialComponent.materials[i].SetTexture(propertyName, sprite.texture);
            }
        }
    }

    public class MaterialComponent : IComponent
    {
        public Material[] materials;
    }
}