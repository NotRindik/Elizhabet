using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Systems;

[CreateAssetMenu(menuName = "Animation/Composer Config")]
public class AnimationComposerConfig : ScriptableObject
{
    [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, ShowFoldout = false)]
    public List<AnimationStateConfig> states = new();
}