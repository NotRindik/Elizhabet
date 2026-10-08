using Sirenix.OdinInspector;
using UnityEngine;

public class Chest : BoolStateObject
{
    public Rigidbody2D[] prefabs;
    public AnimationSystem animationSystem;
    public Transform spawnPos;

    public float force = 7f;

    [MinMaxSlider(-10, 10)]
    public Vector2 rangeX = new Vector2(-1.5f, 1.5f);

    public BetterEvent onChestOpened;
    public BetterEvent onImmediateOpen;

    public bool isOpened;

    private void OnValidate()
    {
        animationSystem ??= GetComponent<AnimationSystem>();
    }

    protected override void OnLoaded() => OpenImmediate();

    public void OpenImmediate()
    {
        if (isOpened)
            return;

        animationSystem.Play("OpenChest", true);
        isOpened = true;
        onImmediateOpen.Invoke();
    }

    public void OpenChest()
    {
        if (isOpened)
            return;

        isOpened = true;

        animationSystem.Play("OpenChest");

        animationSystem.onStateEnd = () =>
        {
            foreach (var prefab in prefabs)
            {

                var inst = Instantiate(prefab, spawnPos.position, spawnPos.rotation);

                var rb = inst.GetComponent<Rigidbody2D>();
                float randomX = Random.Range(rangeX.x, rangeX.y);

                rb.AddForce(new Vector2(randomX, force), ForceMode2D.Impulse);
            }

            onChestOpened.Invoke();
            Save(true);
            animationSystem.onStateEnd = null;
        };
    }
}