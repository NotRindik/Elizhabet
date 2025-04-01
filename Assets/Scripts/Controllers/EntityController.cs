using System;
using UnityEngine;

namespace Controllers
{
    public abstract class EntityController : Controller
    {
        public ControllersBaseFields baseFields;
    }

    [Serializable]
    public class ControllersBaseFields
    {
        public Rigidbody2D rb;
        public Collider2D collider;
    }
}