using System;
using Systems;
using UnityEngine;
using UnityEngine.UI;

public sealed class EyeLookAtCursorPreview : MonoBehaviour
{
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RawImage previewImage;

    [Header("Local Offset Range")]
    [SerializeField] private Vector2 minOffset = new(-0.08f, -0.04f);
    [SerializeField] private Vector2 maxOffset = new(0.08f, 0.04f);

    [SerializeField] private float smooth = 15f;
    
    SpriteFlipSystem  flip;

    private void Start()
    {
        var abstractEntity = GetComponent<AbstractEntity>();
        flip = abstractEntity.GetControllerSystem<SpriteFlipSystem>();
    }

    private void LateUpdate()
    {
        if(previewCamera == null || previewImage == null) return;

        var mouse = InputManager.inputActions.UI.Point.ReadValue<Vector2>();
        var t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        Look(leftEye, mouse, t);
        Look(rightEye, mouse, t);
    }

    private Vector2 ToScreen(Vector3 world)
    {
        var vp = previewCamera.WorldToViewportPoint(world);
        var rt = previewImage.rectTransform;
        var r = rt.rect;
        var local = new Vector2(r.xMin + vp.x * r.width, r.yMin + vp.y * r.height);
        var canvas = previewImage.canvas;
        var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(uiCam, rt.TransformPoint(local));
    }

    private void Look(Transform eye, Vector2 mouse, float t)
    {
        var socket = eye.parent;
        var delta = mouse - ToScreen(socket.position);

        var dir = new Vector3(
            delta.x / (Screen.width * 0.5f),
            delta.y / (Screen.height * 0.5f),
            0f
        );

        var local = socket.InverseTransformDirection(dir);
        var ls = socket.lossyScale;

        if (ls.x < 0) local.x = -local.x;
        if (ls.y < 0) local.y = -local.y;

        var normalized = Vector2.ClampMagnitude(
            new Vector2(local.x, local.y), 1f);

        var offset = new Vector3(
            Mathf.Lerp(minOffset.x, maxOffset.x, normalized.x * 0.5f + 0.5f),
            Mathf.Lerp(minOffset.y, maxOffset.y, normalized.y * 0.5f + 0.5f),
            0f);
        
        const float flipThreshold = 0.3f;

        if (Mathf.Abs(dir.x) > flipThreshold)
            flip.SetFacing((int)Mathf.Sign(dir.x));

        eye.localPosition = Vector3.Lerp(
            eye.localPosition,
            offset,
            t
        );
    }
}