using std;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_EDITOR
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
                Debug.Log("Exited Play Mode");
                break;
            case PlayModeStateChange.ExitingEditMode:
                Debug.Log("Preparing to enter Play Mode");
                break;
            case PlayModeStateChange.EnteredPlayMode:
                Debug.Log("Entered Play Mode");
                break;
            case PlayModeStateChange.ExitingPlayMode:
                Debug.Log("Preparing to exit Play Mode");
                Allocator.CleanAll();
                break;
        }
    }
}
#endif