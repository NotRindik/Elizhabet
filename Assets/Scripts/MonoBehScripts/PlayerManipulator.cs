using System;
using Assets.Scripts.Systems;
using Controllers;
using Systems;
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

    public void SetHealth(float val)
    {
        player.GetControllerSystem<HealthSystem>().SetHealth(val);
    }
    
    [IngameDebugConsole.ConsoleMethod("inventory_enabled","Enable/Disable Inventory")]
    public static void InventoryEnabledCommand(bool val)
    {
        SaveManager.Instance.GetModule<GlobalSaves>().SetData("InventoryActive",$"{val.ToInt32()}").Save();
    }
    
    public void InventoryEnabled(bool val)
    {
        SaveManager.Instance.GetModule<GlobalSaves>().SetData("InventoryActive",$"{val.ToInt32()}").Save();
    }
    
    public void SetInventorySize(int size)
    {
        player.GetControllerComponent<InventoryComponent>().inventorySize = size;
        SaveManager.Instance.GetModule<GlobalSaves>().SetData("InvSize",$"{size}").Save();
    }
    
    public void SendMassage(string val)
    {
        NotflicationManager.Instance.Send(val);
    }
    public void HealToMax() => player.GetControllerSystem<HealthSystem>().HealToMax();
    public void Heal(float val) => player.GetControllerSystem<HealthSystem>().Heal(val);

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

    public void FreezePlayer(bool isFreeze)
    {
        var provider = player.GetControllerSystem<ProxyInputState>();
        provider.GetState().Move.Update(false,Vector2.zero);
        provider.GetState().Move.Enabled = !isFreeze;
        provider.GetState().Jump.Enabled = !isFreeze;
        ResetVelocity();
    }

    public void SetPlayerPetActive(bool isActive)
    {
        SaveManager.Instance.GetModule<GlobalSaves>().SetData("IsActivePet", isActive.ToInt32().ToString()).Save();
    }
    
    public void SetPet(AbstractEntity pet)
    {
        player.GetControllerComponent<ModificatorsComponent>().GetModSystem<PetsModification>().SetPet(pet);
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