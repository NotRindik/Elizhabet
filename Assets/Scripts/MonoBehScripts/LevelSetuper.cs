using Controllers;
using UnityEngine;

[DefaultExecutionOrder(10)]
public class LevelSetuper : MonoBehaviour
{
    private void Start()
    {
        if (!string.IsNullOrEmpty(SceneLoader.pendingEntry))
        {
            var pending = SceneEntryRegistry.Instance.Get(SceneLoader.pendingEntry);
            var rb = ContextManager.Instance.player.GetControllerComponent<ControllersBaseFields>().rb;
            rb.position = pending.SpawnPos.position; //TODO учитывать загрузку игрока
            rb.linearVelocity = Vector2.zero;
        }
    }
}
