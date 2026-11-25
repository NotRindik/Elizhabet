using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SaveIdSync
{
    static readonly Type[] OwnerTypes =
    {
        typeof(BoolStateObject),
        typeof(CutsceneManager),
        typeof(TriggerEvent),
        typeof(RepositionSave),
        typeof(VideoPlayerManager),
        typeof(UniversalInteractor),
        typeof(SaveListener),
        typeof(SaveCapsule),
    };

    static SaveIdSync()
    {
        EditorSceneManager.sceneSaving += (scene, path) => Sync(scene, true);
        EditorSceneManager.sceneSaved += scene =>
        {
            if (Sync(scene, false) > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"SaveId: new objects in '{scene.name}' got their ids only after saving — save the scene once more.");
            }
        };
    }

    [MenuItem("Tools/Eliza/Save IDs/Sync Open Scenes")]
    static void SyncOpenScenes()
    {
        int total = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            int changed = Sync(scene, true);
            if (changed > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            total += changed;
        }
        Debug.Log($"SaveId: {total} changes in open scenes. Save the scenes to keep them.");
    }

    public static int Sync(Scene scene, bool addMissing)
    {
        if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
            return 0;
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene == scene)
            return 0;

        int changed = 0;
        var roots = scene.GetRootGameObjects();

        if (addMissing)
        {
            var owners = new HashSet<GameObject>();
            foreach (var root in roots)
                foreach (var type in OwnerTypes)
                    foreach (var c in root.GetComponentsInChildren(type, true))
                        owners.Add(c.gameObject);

            foreach (var go in owners)
            {
                if (go.GetComponent<SaveId>() != null)
                    continue;
                Undo.AddComponent<SaveId>(go);
                changed++;
            }
        }

        foreach (var root in roots)
            foreach (var saveId in root.GetComponentsInChildren<SaveId>(true))
                if (Apply(saveId))
                    changed++;

        return changed;
    }

    static bool Apply(SaveId saveId)
    {
        var value = Compute(saveId.gameObject);
        if (saveId.Id == value)
            return false;

        var so = new SerializedObject(saveId);
        so.FindProperty("id").stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        saveId.EditorSetId(value);
        return true;
    }

    public static string Compute(GameObject go)
    {
        var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
        if (gid.targetObjectId == 0)
            return string.Empty;
        return gid.targetPrefabId == 0
            ? gid.targetObjectId.ToString()
            : $"{gid.targetObjectId}_{gid.targetPrefabId}";
    }
}
