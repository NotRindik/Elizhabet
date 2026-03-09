using Controllers;
using UnityEngine;

public class PlayerManipulator : MonoBehaviour
{
    public PlayerController player => ContextManager.Instance.player;

    /* ---------------- POSITION ---------------- */

    public void SetPosition(Vector3 position)
    {
        player.transform.position = position;
    }

    public void Teleport(Transform point)
    {
        player.transform.position = point.position;
        player.transform.rotation = point.rotation;
    }

    public void SetRotation(Vector3 euler)
    {
        player.transform.rotation = Quaternion.Euler(euler);
    }

    /* ---------------- ENABLE / DISABLE ---------------- */

    public void EnablePlayer()
    {
        player.gameObject.SetActive(true);
    }

    public void DisablePlayer()
    {
        player.gameObject.SetActive(false);
    }

    /* ---------------- CONTROL ---------------- */

    public void EnableControl()
    {
        player.enabled = true;
    }

    public void DisableControl()
    {
        player.enabled = false;
    }

    /* ---------------- PHYSICS ---------------- */

    public void ResetVelocity()
    {
        if (player.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /* ---------------- ANIMATION ---------------- */

    public void PlayAnimation(string anim)
    {
        if (player.TryGetComponent<Animator>(out var animator))
        {
            animator.Play(anim);
        }
    }

    /* ---------------- LOOK ---------------- */

    public void LookAt(Transform target)
    {
        Vector3 dir = target.position - player.transform.position;
        dir.y = 0;

        if (dir != Vector3.zero)
            player.transform.rotation = Quaternion.LookRotation(dir);
    }
}