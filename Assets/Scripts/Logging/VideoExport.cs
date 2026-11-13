using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Videos of a trial from fixed cameras, e.g. the same scenario as stimuli for an online
/// (crowdsourced) study, in the editor, in builds and in batch mode: each camera renders offscreen
/// at a fixed frame rate (the simulation runs in step with the frames: Time.captureFramerate) to
/// JPEG frames, which ffmpeg (on the PATH) joins into ExperimentLogs/Videos/{prefix}{camera}.mp4.
/// Cameras: "name=x,y,z,yaw,pitch[,fov]" in the world, or "name=chase" / "name=front" / "name=driver"
/// relative to the participant (behind, ahead looking back, the participant's own view).
/// Command line: -videoCams "street=0,1.6,-20,0,0;chase" -videoSeconds 10 -videoStart 2 -videoFps 30
/// -videoSize 1280x720 [-videoQuit] [-videoKeepFrames] [-ffmpeg path/to/ffmpeg].
/// </summary>
public class VideoExport : MonoBehaviour
{
    public string Cameras = "";
    public float Start = 0f, Seconds = 10f;
    public int Fps = 30;
    public Vector2Int Size = new Vector2Int(1280, 720);
    public bool QuitWhenDone, KeepFrames;

    public static VideoExport Ensure()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-videoCams");
        if (i < 0 || i + 1 >= args.Length) return null;
        var go = new GameObject("Video export");
        var v = go.AddComponent<VideoExport>();
        v.Cameras = args[i + 1];
        var ci = CultureInfo.InvariantCulture;
        string A(string k) { int j = Array.IndexOf(args, k); return j >= 0 && j + 1 < args.Length ? args[j + 1] : null; }
        if (float.TryParse(A("-videoSeconds"), NumberStyles.Float, ci, out var s)) v.Seconds = s;
        if (float.TryParse(A("-videoStart"), NumberStyles.Float, ci, out var st)) v.Start = st;
        if (int.TryParse(A("-videoFps"), out var f)) v.Fps = f;
        var size = A("-videoSize");
        if (size != null && size.Split('x') is var wh && wh.Length == 2 && int.TryParse(wh[0], out var w) && int.TryParse(wh[1], out var h)) v.Size = new Vector2Int(w, h);
        v.QuitWhenDone = Array.IndexOf(args, "-videoQuit") >= 0;
        v.KeepFrames = Array.IndexOf(args, "-videoKeepFrames") >= 0;
        return v;
    }

    sealed class Shot { public string Name, Spec; public Camera Cam; public string Dir; public int Frames; }

    IEnumerator Start_()
    {
        yield return null;
        var ci = CultureInfo.InvariantCulture;
        float t0 = Time.time;
        while (Time.time - t0 < Start) yield return null;
        var shots = new List<Shot>();
        var root = Path.Combine("ExperimentLogs", "Videos");
        string stamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
        int k = 0;
        foreach (var part in Cameras.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var nv = part.Split('=');
            string name = nv.Length > 1 ? nv[0].Trim() : (++k == 1 ? "camera" : "camera" + k);
            string spec = (nv.Length > 1 ? nv[1] : nv[0]).Trim();
            var go = new GameObject("Video camera " + name);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.targetTexture = new RenderTexture(Size.x, Size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var dir = Path.Combine(root, $"{NetworkingManager.LogPrefix}{name}-{stamp}");
            Directory.CreateDirectory(dir);
            shots.Add(new Shot { Name = name, Spec = spec, Cam = cam, Dir = dir });
        }
        Time.captureFramerate = Fps;
        var tex = new Texture2D(Size.x, Size.y, TextureFormat.RGB24, false);
        int frames = Mathf.RoundToInt(Seconds * Fps);
        Debug.Log($"VideoExport: {shots.Count} cameras, {frames} frames at {Fps} fps, {Size.x}x{Size.y}");
        for (int f = 0; f < frames; f++)
        {
            yield return null;   // (WaitForEndOfFrame never comes in batch mode; the cameras render on demand)
            foreach (var s in shots)
            {
                Place(s);
                s.Cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = s.Cam.targetTexture;
                tex.ReadPixels(new Rect(0, 0, Size.x, Size.y), 0, 0, false);
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(s.Dir, $"frame_{f:D6}.jpg"), tex.EncodeToJPG(92));
                s.Frames++;
            }
        }
        Time.captureFramerate = 0;
        foreach (var s in shots)
        {
            var mp4 = s.Dir + ".mp4";
            if (Encode(s.Dir, mp4))
            {
                Debug.Log($"VideoExport: {mp4} ({s.Frames} frames)");
                if (!KeepFrames) Directory.Delete(s.Dir, true);
            }
            else Debug.Log($"VideoExport: frames in {s.Dir} (ffmpeg not found; join them with: ffmpeg -framerate {Fps} -i frame_%06d.jpg -pix_fmt yuv420p {s.Name}.mp4)");
            Destroy(s.Cam.targetTexture); Destroy(s.Cam.gameObject);
        }
        if (QuitWhenDone) Application.Quit();
    }

    void Awake() => StartCoroutine(Start_());

    void Place(Shot s)
    {
        var t = s.Cam.transform;
        var ps = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem : null;
        var me = ps != null ? ps.LocalPlayer : null;
        switch (s.Spec.ToLowerInvariant())
        {
            case "chase" when me != null:
                t.position = me.transform.position - me.transform.forward * 5f + Vector3.up * 2.2f;
                t.LookAt(me.transform.position + Vector3.up * 0.9f + me.transform.forward * 2f);
                s.Cam.fieldOfView = 60f;
                return;
            case "front" when me != null:
                t.position = me.transform.position + me.transform.forward * 9f + me.transform.right * 1.2f + Vector3.up * 1.6f;
                t.LookAt(me.transform.position + Vector3.up * 0.8f);
                s.Cam.fieldOfView = 50f;
                return;
            case "driver" when me != null:
                Camera own = null;
                foreach (var c in me.GetComponentsInChildren<Camera>()) if (c.isActiveAndEnabled && c.targetTexture == null && (own == null || c.depth > own.depth)) own = c;
                if (own != null) { t.SetPositionAndRotation(own.transform.position, own.transform.rotation); s.Cam.fieldOfView = own.fieldOfView; }
                return;
        }
        var v = s.Spec.Split(',');
        var ci = CultureInfo.InvariantCulture;
        float F(int i, float d) => i < v.Length && float.TryParse(v[i], NumberStyles.Float, ci, out var x) ? x : d;
        t.SetPositionAndRotation(new Vector3(F(0, 0), F(1, 1.6f), F(2, 0)), Quaternion.Euler(F(4, 0), F(3, 0), 0));
        s.Cam.fieldOfView = F(5, 60f);
    }

    bool Encode(string dir, string mp4)
    {
        var args = Environment.GetCommandLineArgs();
        int a = Array.IndexOf(args, "-ffmpeg");
        var candidates = new List<string>();
        if (a >= 0 && a + 1 < args.Length) candidates.Add(args[a + 1]);
        candidates.AddRange(new[] { "ffmpeg", "/usr/local/bin/ffmpeg", "/opt/homebrew/bin/ffmpeg", @"C:\ffmpeg\bin\ffmpeg.exe" });
        foreach (var exe in candidates)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(exe, $"-y -loglevel error -framerate {Fps} -i \"{Path.Combine(dir, "frame_%06d.jpg")}\" -c:v libx264 -pix_fmt yuv420p -crf 18 \"{mp4}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
                p.WaitForExit(600000);
                if (p.ExitCode == 0 && File.Exists(mp4)) return true;
            }
            catch (Exception) { }
        }
        return false;
    }
}
