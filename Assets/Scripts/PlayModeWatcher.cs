#if UNITY_EDITOR
using std;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
[InitializeOnLoad]
public static class PlayModeWatcher
{
    static PlayModeWatcher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredEditMode:
                Debug.Log("Exited Init Mode");
                break;
            case PlayModeStateChange.ExitingEditMode:
                Debug.Log("Preparing to enter Init Mode");
                break;
            case PlayModeStateChange.EnteredPlayMode:
                Debug.Log("Entered Init Mode");
                break;
            case PlayModeStateChange.ExitingPlayMode:
                Debug.Log("Preparing to exit Init Mode");
                Allocator.CleanAll();
                break;
        }
    }
}
#endif