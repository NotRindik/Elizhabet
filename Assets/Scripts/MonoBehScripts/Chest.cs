using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

public class Chest : SerializedMonoBehaviour
{
    public Rigidbody2D[] prefabs;
    public AnimationSystem animationSystem;
    public Transform spawnPos;

    public float force = 7f;
    
    [MinMaxSlider(-10,10)]
    public Vector2 rangeX = new Vector2(-1.5f,1.5f);

    public string localKey = "Chest";
    
    private string Key => WorldKeyBuilder.Build(this,localKey);
    
    public BetterEvent onChestOpened;
    public BetterEvent onImmediateOpen;

    public bool isSaved;
    
    public bool isOpened;


    private void Start()
    {
        isSaved = SaveManager.Instance.GetModule<WorldObjectsStateSave>().Exist(Key);

        if (isSaved)
        {
            OpenImmediate();
        }
    }
    private void OnValidate()
    {
        animationSystem ??= GetComponent<AnimationSystem>();
    }

    public void OpenImmediate()
    {
        if(isOpened)
            return;
        animationSystem.Play("OpenChest",true);
        isOpened = true;
    }

    public void OpenChest()
    {
        if(isOpened)
            return;
        
        isOpened = true;
        
        animationSystem.Play("OpenChest");

        animationSystem.onStateEnd = () =>
        {
            foreach (var prefab in prefabs)
            {
                onChestOpened.Invoke();
                
                var inst = Instantiate(prefab, spawnPos.position, spawnPos.rotation);

                var rb = inst.GetComponent<Rigidbody2D>();
                float randomX = Random.Range(rangeX.x, rangeX.y);

                Vector2 res = new Vector2(randomX, this.force);

                rb.AddForce(res, ForceMode2D.Impulse);
            }

            SaveManager.Instance.GetModule<WorldObjectsStateSave>().SetData(Key,"1").Save();
            animationSystem.onStateEnd = null;
        };
        
    }
}
