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
        activeNt = StartCoroutine(SendNotification(text));
    }

    public IEnumerator SendNotification(string text)
    {
        float maxWidth = 600f;
        float minWidth = 200f;
        tmpText.text = text;
        
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth);
        tmpText.ForceMeshUpdate();

        Vector2 padding = new Vector2(60, 20) * 2;

        float preferredWidth = Mathf.Clamp(tmpText.preferredWidth + padding.x, minWidth, maxWidth);
        float preferredHeight = tmpText.preferredHeight + padding.y;
        
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(preferredWidth, preferredHeight);

        animator.Play("Appear");
        yield return new WaitForSecondsRealtime(5);
        animator.Play("Disappear");
        activeNt = null;
    }
}
