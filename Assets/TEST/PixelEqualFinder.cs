using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

[ExecuteAlways]
public class PixelEqualFinder : MonoBehaviour
{
    public string imagePath;
    public bool isActive = false;
    public Texture2D newTexture;

    private void Update()
    {
        if (isActive)
        {
            CheckForDuplicates(imagePath);
            isActive = false;
        }
    }

    void CheckForDuplicates(string path)
    {
        // Загрузка текстуры
        byte[] fileData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);

        Dictionary<Color32,Vector2Int> uniquePixels = new Dictionary<Color32, Vector2Int>();

        // Проверка всех пикселей
        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
            {
                Color32 pixel = texture.GetPixel(x, y);
                if (pixel.a != 255)
                    continue;
                if (uniquePixels.ContainsKey(pixel))
                {
                    if (newTexture == null)
                    {
                        newTexture = new Texture2D(texture.width, texture.height);
                        newTexture.name = texture.name + 1;
                        newTexture.SetPixels(texture.GetPixels());
                    }
                    Color32 newcolor = new Color32((byte)Random.Range(0, 255), (byte)Random.Range(0, 255), 0, 255);
                    newTexture.SetPixel(x, y, newcolor);
                    newTexture.Apply();
                    string pathToSave =  $"Assets/Resources/Animation/Clone/{texture.name + 1}.png"; // Укажи путь, где хочешь сохранить
                    byte[] textureBytes = newTexture.EncodeToPNG();
                    File.WriteAllBytes(pathToSave, textureBytes);
                    AssetDatabase.ImportAsset(pathToSave); // Импортируем новый файл в Unity

                    Debug.LogWarning($"Duplicate pixel found at: ({x},{y}), it dublicate On ({uniquePixels[pixel].x},{uniquePixels[pixel].y}) and color is {pixel} but it was replaced on {newcolor}");
                }
                else
                {
                    uniquePixels.Add(pixel,new Vector2Int(x,y));
                }
            }
        }
    }
}
