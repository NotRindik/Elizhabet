using Systems;
using UnityEngine;
using System.Collections.Generic;

public class MaterialSystemWrapper : MonoBehaviour
{
    public Material[] materials;

    private Dictionary<Material, Dictionary<string, Texture>> originalTextures;

    private void Awake()
    {
        originalTextures = new Dictionary<Material, Dictionary<string, Texture>>();
        for (int i = 0; i < materials.Length; i++)
        {
            Material mat = materials[i];
            var textures = new Dictionary<string, Texture>();

            textures["_LUT"] = mat.GetTexture("_LUT");
            for (int j = 1; j < 11; j++)
            {
                string prop = $"_LUT{j}";
                if (mat.HasProperty(prop))
                    textures[prop] = mat.GetTexture(prop);
            }

            originalTextures[mat] = textures;
        }
    }

    public void AddTexture(int index, Sprite sprite)
    {
        string propertyName = index switch
        {
            0 => "_LUT",
            _ => $"_LUT{index}"
        };
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].SetTexture(propertyName, sprite.texture);
        }
    }

    private void OnDestroy()
    {
        print("ABOBA");
        foreach (var kvp in originalTextures)
        {
            Material mat = kvp.Key;
            foreach (var texKvp in kvp.Value)
            {
                mat.SetTexture(texKvp.Key, texKvp.Value);
            }
        }
    }
}