
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace  Systems
{
    public class InteractionHandleSystem : BaseSystem,IDisposable
    {
        [FormerlySerializedAs("InteractionHandleComponent")] public InteractionHandleComponent InteractC;
        public IInputProvider InputProvider;
        public Action<InputContext> OnInteract;
        private ContactFilter2D _filter;

        private Collider2D _nearestCol;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            InteractC = owner.GetControllerComponent<InteractionHandleComponent>();
            InputProvider = base.owner.GetControllerSystem<IInputProvider>();

            OnInteract = _ => Update();

            InputProvider.GetState().Interact.performed += OnInteract;

            owner.OnUpdate += UpdateHitData;
            InteractC.hitedCols = new Collider2D[10];
            
            
            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = InteractC.interactMask,
                useTriggers = true
            };
        }

        public override void OnUpdate()
        {
            _nearestCol?.GetComponent<IInteractable>()?.Interact(owner);
        }

        public void UpdateHitData()
        {
            if (!IsActive)
                return;

            int len = Physics2D.OverlapCircle(
                transform.position,
                InteractC.interactionRadius,
                _filter,
                InteractC.hitedCols
            );

            if (len == 0)
            {
                if (_nearestCol != null)
                {
                    var oldOutline = _nearestCol.GetComponent<OutLine>();
                    if (oldOutline != null)
                        oldOutline.Disable();
                }
                
                _nearestCol = null;
                return;
            }

            Vector2 pos = transform.position;

            float minDist = float.MaxValue;
            Collider2D nearest = null;

            for (int i = 0; i < len; i++)
            {
                var col = InteractC.hitedCols[i];

                float dist = ((Vector2)col.transform.position - pos).sqrMagnitude;

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = col;
                }
            }

            if (nearest != null && _nearestCol != nearest)
            {
                OutLine outLine = _nearestCol?.GetComponent<OutLine>();
                if (outLine != null)
                {
                    outLine.Disable();
                }
                _nearestCol = nearest;
                
                outLine = nearest?.GetComponent<OutLine>();
                
                if (outLine != null)
                {
                    outLine.Enable();
                }
            }
        }
        public void Dispose()
        {
            owner.OnUpdate -= UpdateHitData;
            InputProvider.GetState().Interact.performed -= OnInteract;
        }
    }

    [System.Serializable]
    public class InteractionHandleComponent : IComponent
    {
        public float interactionRadius;
        public LayerMask interactMask;
        public Collider2D[] hitedCols;
    }
}
