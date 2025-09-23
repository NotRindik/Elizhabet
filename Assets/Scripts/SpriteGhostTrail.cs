using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public unsafe class SpriteGhostTrail : MonoBehaviour
{
    public SpriteRenderer[] spriteRenderers;
    public float ghostLifetime = 0.3f;
    public float spawnInterval = 0.01f;

    private bool isActive = false;
    private int capacity = 10;
    private Coroutine trailHandler;

    private FullObjectParts* pool;
    private void Start()
    {
        pool = (FullObjectParts*)UnsafeUtility.Malloc(
            sizeof(FullObjectParts) * capacity,
            8,
            Unity.Collections.Allocator.Persistent
        );

        for (int i = 0; i < capacity; i++)
        {
            pool[i] = new FullObjectParts(spriteRenderers.Length);
        }
    }

    public void StartTrail()
    {
        if (trailHandler == null)
            trailHandler = StartCoroutine(SpawnTrail());
    }

    IEnumerator SpawnTrail()
    {
        isActive = true;

        while (isActive)
        {
            yield return new WaitForEndOfFrame();
            CreateGhost();
            yield return new WaitForSeconds(spawnInterval);
        }

        trailHandler = null;
    }

    public void StopTrail()
    {
        isActive = false;
    }

    void CreateGhost()
    {
        for (int i = 0; i < capacity; i++)
        {
            if (pool[i].isActive)
                continue;

            pool[i].SetActive(true);

            for (int j = 0; j < spriteRenderers.Length; j++)
            {
                var objectSpritePair = pool[i].pool[j];
                var sr = spriteRenderers[j];
                var poolSr = objectSpritePair.GetSpriteRenderer();
                var pooledObject = objectSpritePair.GetObj();

                pooledObject.SetActive(true);

                poolSr.sprite = sr.sprite;
                poolSr.material = sr.sharedMaterial;
                poolSr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.5f);

                poolSr.flipX = sr.flipX;
                poolSr.transform.position = sr.transform.position;
                poolSr.transform.rotation = sr.transform.rotation;
                poolSr.transform.localScale = sr.transform.lossyScale;
                poolSr.sortingLayerID = sr.sortingLayerID;
                poolSr.sortingOrder = sr.sortingOrder - 10;
            }

            StartCoroutine(TimeToDeactive(pool[i], ghostLifetime));
            break;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < capacity; i++)
        {
            UnsafeUtility.Free(pool[i].pool, Unity.Collections.Allocator.Persistent);
        }

        UnsafeUtility.Free(pool, Unity.Collections.Allocator.Persistent);
    }

    public IEnumerator TimeToDeactive(FullObjectParts @object, float t)
    {
        yield return new WaitForSeconds(t);
        @object.SetActive(false);
    }

    public struct FullObjectParts
    {
        public ObjectPoolData* pool;
        public IntPtr parentObj;

        public bool isActive => GetParentObj().activeSelf;

        public FullObjectParts(int capacity)
        {
            pool = (ObjectPoolData*)UnsafeUtility.Malloc(
                sizeof(ObjectPoolData) * capacity,
                8,
                Unity.Collections.Allocator.Persistent
            );

            parentObj = (IntPtr)new GameObject("GhostSpriteCollection").GetInternalPointer();

            for (int i = 0; i < capacity; i++)
            {
                pool[i] = new ObjectPoolData("Ghost Sprite");
                pool[i].GetObj().transform.SetParent(GetParentObj().transform);
            }

            SetActive(false);
        }

        public void SetActive(bool isActive)
        {
            GetParentObj().SetActive(isActive);
        }

        public GameObject GetParentObj()
        {
            return Unsafe.As<GameObject>(parentObj);
        }
    }

    public struct ObjectPoolData
    {
        public IntPtr objects;
        public IntPtr renderers;

        public ObjectPoolData(string name)
        {
            objects = (IntPtr)new GameObject("GhostSprite").GetInternalPointer();

            renderers = (IntPtr)Unsafe.As<GameObject>(objects).AddComponent<SpriteRenderer>().GetInternalPointer();
        }

        public GameObject GetObj()
        {
            return Unsafe.As<GameObject>(objects);
        }

        public SpriteRenderer GetSpriteRenderer()
        {
            return Unsafe.As<SpriteRenderer>(renderers);
        }
    }
}
