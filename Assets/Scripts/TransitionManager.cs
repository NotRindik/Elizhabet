using Sirenix.OdinInspector;
using System.Collections.Generic;
using NaughtyAttributes;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[CreateAssetMenu(menuName = "Configs/Scene Handle")]
public class SceneHandle : SerializedScriptableObject
{
    [Scene]
    public string sceneAsset;
    
    [ListDrawerSettings(
    ShowPaging = false,
    DraggableItems = false,
    CustomAddFunction = "AddExit",
    CustomRemoveElementFunction = "RemoveExit")]
    [InlineEditor(ObjectFieldMode = InlineEditorObjectFieldModes.Hidden)]
    public List<SceneExitData> exits = new();



    private void AddExit()
    {
#if UNITY_EDITOR
        var exit = ScriptableObject.CreateInstance<SceneExitData>();
        exit.name = $"subasset_{exits.Count}";
        UnityEditor.AssetDatabase.AddObjectToAsset(exit, this);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        exits.Add(exit);
#endif
    }
    private void RemoveExit(SceneExitData r)
    {
#if UNITY_EDITOR
        exits.Remove(r);
        DestroyImmediate(r, true);

        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}

#if UNITY_EDITOR
public static class SceneHandleCreator
{
    [MenuItem("Assets/Create/Game/Scene Handle From Scene", true)]
    private static bool ValidateCreate()
    {
        return Selection.activeObject is SceneAsset;
    }

    [MenuItem("Assets/Create/Game/Scene Handle From Scene")]
    private static void Create()
    {
        var sceneAsset = Selection.activeObject as SceneAsset;

        var handle = ScriptableObject.CreateInstance<SceneHandle>();
        handle.sceneAsset = sceneAsset.name;

        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        string folderPath = System.IO.Path.GetDirectoryName(scenePath);

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            folderPath + "/" + sceneAsset.name + "_Handle.asset");

        AssetDatabase.CreateAsset(handle, assetPath);
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = handle;
    }
}
#endif