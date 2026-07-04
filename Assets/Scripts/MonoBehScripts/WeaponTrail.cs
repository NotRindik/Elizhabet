using Controllers;
using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class WeaponTrail : MonoBehaviour
{
    private TrailRenderer trail;

    private Item weapon;

    public Transform playerRoot => weapon?.GetControllerComponent<ItemComponent>()?.currentOwner?.transform; // �����

    private Vector3 lastPlayerPos;

    public float deselectLifeTime;
    private float selectLifeTime;
    void Start()
    {
        trail = GetComponent<TrailRenderer>();
        weapon = GetComponentInParent<Item>();

        selectLifeTime = trail.time;
        lastPlayerPos = playerRoot == null ? Vector3.zero:playerRoot.position;

        weapon.OnTake += _ => trail.time = selectLifeTime;
        weapon.OnThrow += () => trail.time = deselectLifeTime;
    }

    void LateUpdate()
    {
        if (playerRoot == null)
            return;

        Vector3 playerDelta = playerRoot.position - lastPlayerPos;

        if (playerDelta != Vector3.zero)
        {
            MoveTrail(playerDelta);
        }

        lastPlayerPos = playerRoot.position;
    }

    void MoveTrail(Vector3 offset)
    {
        int count = trail.positionCount;
        if (count == 0) return;

        Vector3[] positions = new Vector3[count];
        trail.GetPositions(positions);

        for (int i = 0; i < count; i++)
            positions[i] += offset;

        trail.SetPositions(positions);
    }
}