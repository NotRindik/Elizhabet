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

        protected override void ReferenceClean()
        {
        }

        protected void Start()
        {
        }
        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position,5);
        }
    }
}
