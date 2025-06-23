using UnityEngine;

namespace Systems
{
    public enum SyncState
    {
        Synced,
        Desynced
    }
    public class PlayerBodySyncSystem: BaseSystem
    {
        public Transform upperBody;
        public Transform lowerBody;

        public SyncState syncState = SyncState.Synced;

        private Vector3 lastLowerBodyOffset;
    }
}