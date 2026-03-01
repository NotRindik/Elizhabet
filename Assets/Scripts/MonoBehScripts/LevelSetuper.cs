using UnityEngine;

[DefaultExecutionOrder(10)]
public class LevelSetuper : MonoBehaviour
{
    private void Start()
    {
        if (!string.IsNullOrEmpty(SceneLoader.pendingEntry))
        {
            var pending = SceneEntryRegistry.Instance.Get(SceneLoader.pendingEntry);
            ContextManager.Instance.player.transform.position = pending.SpawnPos.position;
        }
    }
}
