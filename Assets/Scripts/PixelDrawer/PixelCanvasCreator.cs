using UnityEditor;
using UnityEngine;

public static class PixelCanvasCreator
{
    [MenuItem("GameObject/2D/My Custom Object", false, 10)]
    private static void CreateObject()
    {
        var obj = new GameObject("PixelCanvas");
        var pixelCanvas = obj.AddComponent<PixelCanvas>();
        pixelCanvas.assetFolder = "Assets/Resources/Sprites/WorldArt";
        
        var layer1 = new GameObject("PixelCanvas");
        
        layer1.transform.SetParent(pixelCanvas.transform);
        
        var layer = layer1.AddComponent<PixelLayer>();
        
        
        Undo.RegisterCreatedObjectUndo(obj, "Create PixelCanvas");
        
        GameObjectUtility.SetParentAndAlign(obj, Selection.activeGameObject);

        Selection.activeGameObject = obj;
    }
}
