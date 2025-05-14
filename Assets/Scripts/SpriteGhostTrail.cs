using System.Collections;
using UnityEngine;

public class SpriteGhostTrail : MonoBehaviour
{
    public SpriteRenderer sourceRenderer;
    public float ghostLifetime = 0.3f;
    public float spawnInterval = 0.05f;
    public Color ghostColor = new Color(1f, 1f, 1f, 0.5f); // Полупрозрачный

    private bool isActive = false;

    public void StartTrail()
    {
        if (!isActive)
            StartCoroutine(SpawnTrail());
    }

    IEnumerator SpawnTrail()
    {
        isActive = true;

        while (true)
        {
            CreateGhost();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    public void StopTrail()
    {
        StopAllCoroutines();
        isActive = false;
    }

    void CreateGhost()
    {
        GameObject ghost = new GameObject("GhostSprite");
        SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();

        ghostRenderer.sprite = sourceRenderer.sprite;
        ghostRenderer.material = sourceRenderer.material;
        ghostRenderer.flipX = sourceRenderer.flipX;
        ghostRenderer.transform.position = sourceRenderer.transform.position;
        ghostRenderer.transform.rotation = sourceRenderer.transform.rotation;
        ghostRenderer.transform.localScale = sourceRenderer.transform.lossyScale;
        ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        ghostRenderer.color = ghostColor;
        ghostRenderer.sortingOrder = sourceRenderer.sortingOrder - 10;
        Destroy(ghost, ghostLifetime);
    }
}