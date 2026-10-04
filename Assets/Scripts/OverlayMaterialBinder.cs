using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.UI;
public class OverlayMaterialBinder : MonoBehaviour
{
    [Tooltip("Если true - берём игрока из ContextManager (для UI). Иначе - владельца из родителей (для спрайтов на игроке).")]
    [SerializeField] private bool useCurrentPlayer = true;

    private Material _template;
    private Renderer _renderer;
    private Graphic _graphic;
    private TextureOverlaySystem _system;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _graphic  = GetComponent<Graphic>();
        
        _template = _renderer != null ? _renderer.sharedMaterial : _graphic.material;
    }

    void OnEnable()
    {
        if (useCurrentPlayer)
        {
            EventBus.OnPlayerChange += Rebind;
            Rebind(ContextManager.Instance.player);
        }
        else
        {
            Bind(GetComponentInParent<AbstractEntity>());
        }
    }

    void OnDisable()
    {
        if (useCurrentPlayer && ContextManager.Instance != null)
            EventBus.OnPlayerChange -= Rebind;
        Unbind();
    }

    void Rebind(PlayerController p) => Bind(p);

    void Bind(AbstractEntity entity)
    {
        Unbind();
        if (entity == null) return;

        _system = entity.GetControllerSystem<TextureOverlaySystem>();
        if (_system == null) return;

        _system.InstancesChanged += Apply;
        Apply();
    }

    void Unbind()
    {
        if (_system != null) _system.InstancesChanged -= Apply;
        _system = null;
        SetMaterial(_template);
    }

    void Apply()
    {
        if (_system != null && _system.TryGetInstance(_template, out var inst))
            SetMaterial(inst);
    }

    void SetMaterial(Material m)
    {
        if (_renderer != null) _renderer.sharedMaterial = m;
        else if (_graphic != null) _graphic.material = m;
    }
}