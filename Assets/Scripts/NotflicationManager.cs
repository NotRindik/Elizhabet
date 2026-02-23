using System.Collections;
using TMPro;
using UnityEngine;

interface INotflication
{
    public void Send(string text);
}

public class NotflicationManager : MonoBehaviour, INotflication
{
    public static NotflicationManager Instance;

    public Animator animator;
    public TextMeshProUGUI tmpText;
    public RectTransform rectTransform;
    public Coroutine activeNt;
    private void Awake()
    {
        if(Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Instance = null;
    }


    public void Send(string text)
    {
        if(activeNt != null)
            StopCoroutine(activeNt);
        activeNt = StartCoroutine(SendNotflicaton(text));
    }

    public IEnumerator SendNotflicaton(string text)
    {
        tmpText.text = text;

        // Вычисляем предпочтительный размер текста
        tmpText.ForceMeshUpdate(); // обновляет размеры
        Vector2 textSize = tmpText.GetRenderedValues(false);

        // Добавляем отступы (padding)
        Vector2 padding = new Vector2(60, 20)*2;

        rectTransform.sizeDelta = textSize + padding;

        // Если хочешь расширять вниз:
        rectTransform.pivot = new Vector2(0.5f, 1f); // верхний центр
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y);

        animator.Play("Appear");
        yield return new WaitForSecondsRealtime(5);
        animator.Play("Disappear");
        activeNt = null;
    }
}
