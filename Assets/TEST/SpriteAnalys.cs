using UnityEngine;

[ExecuteAlways]
public class SpriteAnalys : MonoBehaviour
{
    public Texture2D mainTexture;
    public Texture2D skinTexture;

    public Vector2[] RG;
    public Vector2 WH;
    public Vector2[] UV;

    public bool Start;

    private void Update()
    {
        if (Start)
        {
            if (mainTexture == null || skinTexture == null)
                return;
            var pixels = mainTexture.GetPixels();
            RG = new Vector2[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                RG[i] = new Vector2(pixels[i].r, pixels[i].g);
                RG[i] *= 255;
                RG[i] += new Vector2(0.5f, 0.5f);
            }

            WH = new Vector2(skinTexture.width, skinTexture.height);

            UV = new Vector2[RG.Length];
            for (int i = 0; i < RG.Length; i++)
            {
                UV[i] = RG[i] / WH;
            }
            Start = false;
        }

    }
}
