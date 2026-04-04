using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Configs/Scene Exit Data")]
public class SceneExitData : SerializedScriptableObject
{
    [LabelText("SceneExit")]
    public string exit;

    public SceneHandle asset;

    [EnableIf("StartPointEnable")]
    [ValueDropdown("GetAvailableEntries")]
    public string Enter;

    public bool StartPointEnable()
    {
        return asset != null;
    }

    // Функция для ValueDropdown
    private ValueDropdownList<string> GetAvailableEntries()
    {
        var list = new ValueDropdownList<string>();

        if (asset != null && asset.exits != null)
        {
            foreach (var e in asset.exits)
            {
                if (e != null)
                    list.Add(e.exit, e.exit); // отображение и значение одинаковое
            }
        }

        return list;
    }
}