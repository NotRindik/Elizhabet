using System;
using UnityEngine;
using UnityEngine.Tilemaps;

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
            RaycastHit2D hit = Physics2D.Raycast(
                tdc.tileChekPos.position,
                Vector2.down,
                tdc.raydist,
                tdc.layer
            );
            Debug.DrawLine(tdc.tileChekPos.position,hit.point != default ? hit.point :  tdc.tileChekPos.position * Vector2.down * tdc.raydist);

            if (!hit)
            {
                tdc.CurrTile = null;
                return;
            }

            Tilemap tilemap = hit.collider.GetComponentInParent<Tilemap>();

            if (!tilemap)
            {
                tdc.CurrTile = null;
                return;
            }

            Vector3Int cell = tilemap.WorldToCell(
                hit.point + Vector2.down  * 0.01f
            );

            TileBase tile = tilemap.GetTile(cell);

            tdc.CurrTile = tile;

            if (tile != null)
                tile.GetTileData(
                    cell,
                    tilemap,
                    ref tdc.currTileData
                );
        }

        public bool TryGetTileUnderFeet(
            Collider2D groundCollider,
            Vector2 hitPoint,
            out TileBase tileBase)
        {
            tileBase = null;

            if (!groundCollider)
                return false;

            Tilemap tilemap = groundCollider.GetComponentInParent<Tilemap>();

            if (!tilemap)
                return false;

            Vector3Int cellPos = tilemap.WorldToCell(hitPoint);

            tileBase = tilemap.GetTile(cellPos);

            if (tileBase == null)
                return false;

            tileBase.GetTileData(
                cellPos,
                tilemap,
                ref tdc.currTileData
            );

            return true;
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
                if (_currTile == value)
                    return;

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

            sdc.CurrObject = col != null
                ? col.gameObject
                : null;
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