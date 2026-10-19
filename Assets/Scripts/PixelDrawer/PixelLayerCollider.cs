using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(PixelLayer))]
public class PixelLayerCollider : MonoBehaviour
{
    public enum Mode { Polygon, Boxes }

    public const string ChildName = "PixelColliders";

    public Mode mode = Mode.Polygon;

    [Range(1, 255), Tooltip("A pixel counts as solid when its alpha is at least this value.")]
    public int alphaThreshold = 128;

    [Min(1), Tooltip("Precision: size of one collider cell in pixels. 1 = pixel exact, 4 = coarse 4x4 blocks.")]
    public int cellSize = 1;

    [Range(0.05f, 1), Tooltip("Cell size > 1: share of solid pixels a cell needs to count as solid.")]
    public float cellCoverage = 0.5f;

    [Min(0), Tooltip("Polygon mode: outline simplification tolerance in pixels. 0 = exact staircase outline.")]
    public float simplify = 0.5f;

    [Min(1), Tooltip("Connected groups smaller than this many cells are ignored. 1 = keep everything.")]
    public int minIslandCells = 1;

    public bool isTrigger;
    public PhysicsMaterial2D material;

    [Tooltip("Adds CompositeCollider2D (static Rigidbody2D) and merges all shapes into one.")]
    public bool composite;

    [Tooltip("Rebuild automatically when the scene is saved and when entering play mode.")]
    public bool autoRebuild = true;

    [HideInInspector] public int builtHash;
    
    public LayerMask layerMask;
}
