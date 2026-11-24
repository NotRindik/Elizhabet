#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public static class PixelDesktopPicker
{
    const int VkLButton = 0x01;
    const int VkMenu = 0x12;

    const double BounceTime = 0.25;
    const int HoverSnap = 3;

    static bool enabled;
    static bool holding;
    static double releaseTime = -1;
    static bool hasHover;
    static Color32 hoverColor;
    static Point hoverPos;
    static int processId;

    public static bool Supported
    {
        get { return Application.platform == RuntimePlatform.WindowsEditor; }
    }

    public static void Enable()
    {
        if(!Supported || enabled) return;
        enabled = true;
        holding = false;
        hasHover = false;
        processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        GetAsyncKeyState(VkLButton);
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    public static void Disable()
    {
        enabled = false;
        holding = false;
        EditorApplication.update -= Update;
    }

    static void Update()
    {
        if(!enabled)
        {
            EditorApplication.update -= Update;
            return;
        }

        var lmb = GetAsyncKeyState(VkLButton);
        var lmbDown = (lmb & 0x8000) != 0;
        var lmbHit = lmbDown || (lmb & 1) != 0;
        var alt = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
        var now = EditorApplication.timeSinceStartup;

        if(!lmbHit)
        {
            if(holding)
            {
                holding = false;
                releaseTime = now;
            }
            hasHover = false;
            if(!alt) return;
            Point hp;
            if(!GetCursorPos(out hp) || OverUnity(hp)) return;
            hasHover = TryReadColor(hp, out hoverColor);
            hoverPos = hp;
            return;
        }

        if(holding || !alt || now - releaseTime < BounceTime) return;

        Point p;
        if(!GetCursorPos(out p) || OverUnity(p)) return;

        Color32 color;
        var nearHover = hasHover && Mathf.Abs(p.x - hoverPos.x) <= HoverSnap && Mathf.Abs(p.y - hoverPos.y) <= HoverSnap;
        if(nearHover) color = hoverColor;
        else if(!TryReadColor(p, out color)) return;

        holding = true;
        hasHover = false;
        PixelPaintTool.ApplyPickedColor(color);
    }

    static bool OverUnity(Point p)
    {
        var hwnd = WindowFromPoint(p);
        if(hwnd == IntPtr.Zero) return false;
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        return pid == processId;
    }

    static bool TryReadColor(Point p, out Color32 color)
    {
        color = default(Color32);
        var pixels = InternalEditorUtility.ReadScreenPixel(new Vector2(p.x, p.y), 1, 1);
        if(pixels == null || pixels.Length == 0) return false;
        var c = (Color32)pixels[0];
        color = new Color32(c.r, c.g, c.b, 255);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Point
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vk);
}
#endif
