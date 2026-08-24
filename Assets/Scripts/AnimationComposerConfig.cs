using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Systems;

[CreateAssetMenu(menuName = "Animation/Composer Config")]
public class AnimationComposerConfig : ScriptableObject
{
    [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, ShowFoldout = false)]
    public List<AnimationStateConfig> states = new();

    [ListDrawerSettings(ShowFoldout = false, ListElementLabelName = "layerName")]
    public List<AnimationLayerConfig> layers = new();
}

[System.Serializable]
public class AnimationLayerConfig
{
    public string layerName;
    
    public List<string> maskParts = new();

    [ValueDropdown("@$root.states", IsUniqueList = true)]
    [ListDrawerSettings(ShowFoldout = false)]
    [ValidateInput("@HasNoDuplicateStateNames()",
        "В этом слое два состояния с одинаковым stateName — GetState найдёт только первое из них.")]
    public List<AnimationStateConfig> states = new();

    private bool HasNoDuplicateStateNames()
    {
        var seen = new HashSet<string>();
        foreach (var s in states)
        {
            if (s == null) continue;
            if (!seen.Add(s.stateName))
                return false;
        }
        return true;
    }
}