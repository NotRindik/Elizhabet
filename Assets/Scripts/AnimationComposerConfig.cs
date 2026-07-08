using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Systems;

[CreateAssetMenu(menuName = "Animation/Composer Config")]
public class AnimationComposerConfig : ScriptableObject
{
    [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, ShowFoldout = false)]
    public List<AnimationStateConfig> states = new();

    // clip.name вместо строки — клип хранится как ассет-ссылка
    public void LoadInto(AnimationComponentsComposer composer)
    {
        composer.states.Clear();
        foreach (var cfg in states)
        {
            if (cfg == null) continue;
            composer.AddState(cfg.stateName, b =>
            {
                foreach (var p in cfg.parts)
                {
                    if (p.clip == null || string.IsNullOrEmpty(p.partName)) continue;
                    b.Part(p.partName, p.clip.name);
                }
            });
        }
    }
}