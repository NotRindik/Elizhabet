using System;
using System.Collections;
using Systems;
using Unity.VisualScripting;
using UnityEngine;

namespace Controllers
{
    public class EnemyController : EntityController
    {
        public LayerMask lauer;
        protected void Start()
        {
            print("Spawned");
        }
        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position,5);
        }
    }
}
