using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using static Unity.Collections.AllocatorManager;

public class SpriteGhostTrail : MonoBehaviour
{
    public SpriteRenderer[] spriteRenderers;
    public float ghostLifetime = 0.3f;
    public float spawnInterval = 0.01f;
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
        foreach (var spriteRenderer in spriteRenderers)
        {
            GameObject ghost = new GameObject("GhostSprite");
            SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
            ghostRenderer.sprite = spriteRenderer.sprite;

            ghostRenderer.material = new Material(spriteRenderer.sharedMaterial);
            ghostRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 0.5f);

            ghostRenderer.flipX = spriteRenderer.flipX;
            ghostRenderer.transform.position = spriteRenderer.transform.position;
            ghostRenderer.transform.rotation = spriteRenderer.transform.rotation;
            ghostRenderer.transform.localScale = spriteRenderer.transform.lossyScale;
            ghostRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            ghostRenderer.sortingOrder = spriteRenderer.sortingOrder - 10;
            Destroy(ghost, ghostLifetime);   
        }
    }
}