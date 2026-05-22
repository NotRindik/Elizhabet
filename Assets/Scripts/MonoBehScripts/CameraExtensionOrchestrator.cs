using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cinemachine;
public class CameraExtensionOrchestrator : CinemachineExtension
{
    [SerializeField] private MonoBehaviour[] _raw;

    private List<(MonoBehaviour,ICameraExtension)> _sorted = new();

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float dt)
    {
        
        if (stage != CinemachineCore.Stage.Body)
            return;
        foreach (var VARIABLE in _raw)
        {
            if (!_sorted.Contains((VARIABLE,(ICameraExtension)VARIABLE)))
            {
                Rebuild();
            }
        }
        
        foreach (var ext in _sorted)
        {
            if(ext.Item1.enabled)
                ext.Item2.Execute(vcam, stage, ref state, dt);
        }
    }
    private void Rebuild()
    {
        _sorted = _raw
            .Where(m => m != null && m.enabled)
            .Select(m => (mb: m, ext: m as ICameraExtension))
            .Where(x => x.ext != null)
            .OrderBy(x => x.ext.Priority)
            .ToList();
    }
}



public interface ICameraExtension
{
    int Priority { get; }

    void Execute(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float dt);
}