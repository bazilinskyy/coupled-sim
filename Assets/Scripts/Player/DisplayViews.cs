using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Extra views on extra monitors (#39): side screens for a wide field of view (left/right of the
/// main view), rear-view and side mirrors (shown mirrored), or any camera relative to the
/// participant's eyes. Each view goes to a display (2 = the second monitor, ...); the displays are
/// switched on when the participant's avatar is spawned. In the editor, extra displays show in
/// Game views set to Display 2, 3, ... Lives on the Managers object in StartScene.
/// </summary>
public class DisplayViews : MonoBehaviour
{
    public enum ViewKind
    {
        /// <summary>Left of the main view, for a panorama of screens side by side.</summary>
        LeftScreen,
        /// <summary>Right of the main view.</summary>
        RightScreen,
        /// <summary>Behind, from the interior mirror, mirrored.</summary>
        RearMirror,
        /// <summary>Back along the left side of the car, mirrored.</summary>
        LeftMirror,
        /// <summary>Back along the right side of the car, mirrored.</summary>
        RightMirror,
        /// <summary>Offset and rotation from the participant's eyes.</summary>
        Custom,
    }

    [Serializable]
    public struct View
    {
        public ViewKind Kind;
        [Tooltip("Monitor: 2 = the second one, 3 = the third, ...")]
        public int Display;
        [Tooltip("Vertical field of view (degrees); 0 = as the main camera.")]
        public float FieldOfView;
        [Tooltip("Custom: position (m) and rotation (degrees) relative to the eyes. Screens: extra yaw on top of the main view's width.")]
        public Vector3 Offset;
        public Vector3 Rotation;
        [Tooltip("Show the picture mirrored (default for mirrors).")]
        public bool Mirrored;
    }

    public View[] Views = new View[0];
    [Tooltip("Angle between neighbouring screens (degrees); 0 = the main camera's horizontal field of view (seamless).")]
    public float ScreenAngle = 0f;

    void Awake()
    {
        // -views "RearMirror:1,LeftScreen:2,RightScreen:3": views from the command line (replace the inspector's)
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-views");
        if (i < 0 || i + 1 >= args.Length) return;
        var list = new List<View>();
        foreach (var part in args[i + 1].Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':');
            if (!Enum.TryParse(kv[0].Trim(), true, out ViewKind kind)) { Debug.LogWarning("DisplayViews: unknown view " + kv[0]); continue; }
            int display = kv.Length > 1 && int.TryParse(kv[1], out var d) ? d : (kind == ViewKind.RearMirror ? 1 : 2);
            list.Add(new View { Kind = kind, Display = display });
        }
        Views = list.ToArray();
    }

    PlayerAvatar _builtFor;
    readonly List<GameObject> _made = new List<GameObject>();

    void Update()
    {
        var nm = NetworkingManager.Instance;
        var local = nm != null && nm.PlayerSystem != null ? nm.PlayerSystem.LocalPlayer : null;
        if (local == _builtFor) return;
        foreach (var g in _made) if (g != null) Destroy(g);
        _made.Clear();
        _builtFor = local;
        if (local == null || Views == null || Views.Length == 0) return;
        var main = FindMainCamera(local);
        if (main == null) return;
        foreach (var v in Views) Build(v, main, local.transform);
    }

    static Camera FindMainCamera(PlayerAvatar local)
    {
        foreach (var c in local.GetComponentsInChildren<Camera>())
            if (c.isActiveAndEnabled && c.targetTexture == null) return c;
        return Camera.main;
    }

    void Build(View v, Camera main, Transform car)
    {
        int display = Mathf.Max(1, v.Display) - 1;
        if (display > 0 && display < Display.displays.Length && !Display.displays[display].active) Display.displays[display].Activate();
        float fov = v.FieldOfView > 0f ? v.FieldOfView : main.fieldOfView;
        float hfov = Camera.VerticalToHorizontalFieldOfView(main.fieldOfView, main.aspect);
        float step = ScreenAngle > 0f ? ScreenAngle : hfov;
        var go = new GameObject($"View {v.Kind} (display {display + 1})");
        _made.Add(go);
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        cam.fieldOfView = fov;
        cam.depth = main.depth;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        bool mirrored = v.Mirrored;
        switch (v.Kind)
        {
            case ViewKind.LeftScreen:
            case ViewKind.RightScreen:
                go.transform.SetParent(main.transform, false);
                go.transform.localRotation = Quaternion.Euler(v.Rotation + new Vector3(0f, (v.Kind == ViewKind.LeftScreen ? -1f : 1f) * step, 0f));
                break;
            case ViewKind.RearMirror:
                go.transform.SetParent(car, false);
                go.transform.position = main.transform.position + car.forward * 0.5f + car.up * 0.1f + car.TransformVector(v.Offset);
                go.transform.rotation = Quaternion.LookRotation(-car.forward, car.up) * Quaternion.Euler(v.Rotation);
                // look past the driver and the seats, through the rear window
                cam.nearClipPlane = 1.6f;
                if (v.FieldOfView <= 0f) cam.fieldOfView = 30f;
                mirrored = true;
                break;
            case ViewKind.LeftMirror:
            case ViewKind.RightMirror:
                float side = v.Kind == ViewKind.LeftMirror ? -1f : 1f;
                go.transform.SetParent(car, false);
                go.transform.position = main.transform.position + car.right * (side * 0.9f) + car.forward * 0.6f - car.up * 0.15f + car.TransformVector(v.Offset);
                go.transform.rotation = Quaternion.LookRotation(-car.forward + car.right * side * 0.25f, car.up) * Quaternion.Euler(v.Rotation);
                if (v.FieldOfView <= 0f) cam.fieldOfView = 25f;
                mirrored = true;
                break;
            default:
                go.transform.SetParent(main.transform, false);
                go.transform.localPosition = v.Offset;
                go.transform.localRotation = Quaternion.Euler(v.Rotation);
                break;
        }
        if (!mirrored)
        {
            cam.targetDisplay = display;
            return;
        }
        // mirrored: render to a texture shown flipped on that display
        int w = display < Display.displays.Length ? Display.displays[display].renderingWidth : 1280;
        int h = display < Display.displays.Length ? Display.displays[display].renderingHeight : 720;
        var rt = new RenderTexture(Mathf.Max(320, w), Mathf.Max(180, h), 24);
        cam.targetTexture = rt;
        cam.aspect = rt.width / (float)rt.height;
        var canvasGo = new GameObject($"View {v.Kind} screen");
        _made.Add(canvasGo);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = display;
        var img = new GameObject("Image").AddComponent<RawImage>();
        img.transform.SetParent(canvasGo.transform, false);
        img.texture = rt;
        img.uvRect = new Rect(1f, 0f, -1f, 1f);
        var rect = img.rectTransform;
        // on the main display: a mirror at the top of the screen; on other displays: the whole screen
        rect.anchorMin = display == 0 ? new Vector2(0.36f, 0.8f) : Vector2.zero;
        rect.anchorMax = display == 0 ? new Vector2(0.64f, 0.97f) : Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
