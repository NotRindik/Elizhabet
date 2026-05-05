using UnityEngine;

public class WorldUISpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private string key;
    [SerializeField] private Transform followTarget;   // если null — берёт this.transform
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 2f, 0);
    [SerializeField] private string text;

    [Header("Tween Settings")]
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float delay = 0f;
    [SerializeField] private float moveUp = 80f;
    [SerializeField] private float fadeOut = 0.4f;
    [SerializeField] private bool fadeIn = false;

    private WorldUIElement _activeElement;

    public void Spawn()
    {
        var target = followTarget != null ? followTarget : transform;

        var tween = WorldUIManager.Instance
            .Spawn(key, target, string.IsNullOrEmpty(text) ? null : text);

        tween.Duration(duration);

        if (delay > 0)        tween.Delay(delay);
        if (moveUp != 0)      tween.MoveUp(moveUp);
        if (fadeOut > 0)      tween.FadeOut(fadeOut);
        if (fadeIn)           tween.FadeIn();

        _activeElement = tween.Play();
    }

    // Удобные shorthand-методы для вызова из BetterEvent / UnityEvent
    public void SpawnWithText(string overrideText)
    {
        var target = followTarget != null ? followTarget : transform;

        _activeElement = WorldUIManager.Instance
            .Spawn(key, target, overrideText)
            .Duration(duration)
            .Delay(delay)
            .MoveUp(moveUp)
            .FadeOut(fadeOut)
            .Play();
    }

    public void SpawnAtPosition(Vector3 worldPos)
    {
        _activeElement = WorldUIManager.Instance
            .Spawn(key, worldPos + worldOffset, string.IsNullOrEmpty(text) ? null : text)
            .Duration(duration)
            .MoveUp(moveUp)
            .FadeOut(fadeOut)
            .Play();
    }

    public void Kill()
    {
        _activeElement?.Kill();
        _activeElement = null;
    }
}