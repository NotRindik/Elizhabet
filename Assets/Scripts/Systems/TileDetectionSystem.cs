using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using UnityEngine.WSA;

namespace Systems
{
    public class TileDetectionSystem : BaseSystem, IDisposable
    {
        public TileDetectionComponent tdc;

        public void Dispose()
        {
            owner.OnUpdate -= Update;
        }

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            tdc = owner.GetControllerComponent<TileDetectionComponent>();

            owner.OnUpdate += Update;
        }


        public override void OnUpdate()
        {
            Collider2D col = Physics2D.OverlapCircle(tdc.tileChekPos.position,tdc.raydist,tdc.layer);
            if (TryGetTileUnderFeet(col, tdc.tileChekPos.position,out var TileBase))
            {
                tdc.CurrTile = TileBase;
            }
        }

        public bool TryGetTileUnderFeet(Collider2D groundCollider, Vector2 feetWorldPos, out TileBase tileBase)
        {
            tileBase = null;

            if (groundCollider == null)
                return false;

            Tilemap tilemap = groundCollider.GetComponentInParent<Tilemap>();
            if (tilemap == null)
                return false;
            feetWorldPos += Vector2.up * (tilemap.layoutGrid.cellSize.y * 0.1f);

            Vector3Int cellPos = tilemap.WorldToCell(feetWorldPos);
            tileBase = tilemap.GetTile(cellPos);
            tileBase?.GetTileData(cellPos, tilemap, ref tdc.currTileData);
            return tileBase != null;
        }
    }

    [System.Serializable]
    public class TileDetectionComponent : IComponent
    {
        public Transform tileChekPos;
        public float raydist;
        public LayerMask layer;

        [SerializeField] private TileBase _currTile;
         public TileBase CurrTile
         {
             get => _currTile;
             set
             {
                 _currTile = value;
                 OnTileChange?.Invoke(_currTile);
             }
         }
        public TileData currTileData;

        public Action<TileBase> OnTileChange;
    }
    
    public class SurfaceObjectDetectionSystem : BaseSystem
    {
        public SurfaceDetectionComponent sdc;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            sdc = owner.GetControllerComponent<SurfaceDetectionComponent>();
            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {
            Collider2D col = Physics2D.OverlapCircle(
                sdc.checkPos.position,
                sdc.radius,
                sdc.layer
            );

            if (col != null)
            {
                sdc.CurrObject = col.gameObject;
            }
        }
    }
    
    [System.Serializable]
    public class SurfaceDetectionComponent : IComponent
    {
        public Transform checkPos;
        public float radius;
        public LayerMask layer;

        public GameObject CurrObject;
    }
}