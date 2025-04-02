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
            StartCoroutine(DAmAGe());
        }
        private IEnumerator DAmAGe()
        {
            while (true)
            {
                Collider2D other = Physics2D.OverlapCircle(transform.position,5,lauer);
                if (other)
                {
                    if (other.gameObject.TryGetComponent(out PlayerController playerController))
                    {
                        Debug.Log("Damaga");
                        HealthSystem healthSystem = playerController.GetControllerSystem<HealthSystem>();
                        healthSystem.TakeHit(0.1f);
                    }   
                }
                yield return new WaitForSeconds(0.1f);   
            }
        }
        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position,5);
        }
    }
}
