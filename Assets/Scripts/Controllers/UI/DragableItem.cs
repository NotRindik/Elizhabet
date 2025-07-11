using System.Collections;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragableItem : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
{
    public ItemStack itemData;
    public float draggingSpeed;

    private Transform _parentAfterDrag;

    public Transform parentAfterDrag
    {
        get => _parentAfterDrag;
        set
        {
            _parentAfterDrag = value;
            StartDragAnimation();
        }
    }
    public Image image;
    public int slotIndex;
    public Coroutine DragAnimationProcess;
    public void OnBeginDrag(PointerEventData eventData)
    {
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        image.raycastTarget = false;
    }
    public void OnDrag(PointerEventData eventData)
    {
        transform.position = UnityEngine.Input.mousePosition;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        StartDragAnimation();
        image.raycastTarget = true;
    }

    public void StartDragAnimation()
    {
        if(DragAnimationProcess != null)
            StopCoroutine(DragAnimationProcess);
        DragAnimationProcess = StartCoroutine(DragAnimation());
    }

    public IEnumerator DragAnimation()
    {
        while (Vector2.Distance(parentAfterDrag.position, transform.position) > 0.2f)
        {
            yield return new WaitForFixedUpdate();
            transform.position = Vector2.MoveTowards(transform.position, parentAfterDrag.position, draggingSpeed);
        }
        DragAnimationProcess = null;
        transform.SetParent(parentAfterDrag);
    }
}
