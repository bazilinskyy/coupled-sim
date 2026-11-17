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
/// "name=rel:x,y,z,yaw,pitch[,fov]" relative to the participant (its heading only, so a drone's tilt does not shake
/// the picture; smoothed), "name=grid" (the participants' own views in a 2x2 grid, in a replay or with several
/// participants), and the sound at the listener, rendered offline in step with the frames (AudioRenderer), so the
/// video has the simulator's sound in sync however slowly the frames render; -videoAudio 0 leaves it out.
/// Command line: -videoCams "street=0,1.6,-20,0,0;chase" -videoSeconds 10 -videoStart 2 -videoFps 30
/// -videoSize 1280x720 [-videoCrf 18] [-videoAudio 0] [-videoQuit] [-videoKeepFrames] [-ffmpeg path/to/ffmpeg].
/// </summary>
public class VideoExport : MonoBehaviour
{
    public string Cameras = "";
    public float Start = 0f, Seconds = 10f;
    public int Fps = 30;
    public Vector2Int Size = new Vector2Int(1280, 720);
    public bool QuitWhenDone, KeepFrames;
    public bool Audio = true;
    [Tooltip("Hear the scene at the first video camera (e.g. a chase view) instead of at the participant's ears.")]
    public bool ListenAtCamera;
    [Tooltip("x264 quality (18: nearly lossless, 28: small files for the web).")]
    public int Crf = 18;

    /// <summary>A video is being made (the smoke test waits for it).</summary>
    public static bool Busy { get; private set; }

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
        if (int.TryParse(A("-videoCrf"), out var crf)) v.Crf = crf;
        if (A("-videoAudio") == "0") v.Audio = false;
        v.ListenAtCamera = A("-videoListener") == "1";
        Busy = true;
        v.KeepFrames = Array.IndexOf(args, "-videoKeepFrames") >= 0;
        return v;
    }

    sealed class Shot { public string Name, Spec; public Camera Cam; public string Dir; public int Frames; public Camera[] Grid; }

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
            var shot = new Shot { Name = name, Spec = spec, Cam = cam, Dir = dir };
            if (spec.ToLowerInvariant() == "grid")
            {
                shot.Grid = new Camera[4];
                for (int g = 0; g < 4; g++)
                {
                    var gc = new GameObject($"Video camera {name} {g + 1}").AddComponent<Camera>();
                    gc.enabled = false; gc.nearClipPlane = 0.05f; gc.fieldOfView = 70f;
                    gc.targetTexture = new RenderTexture(Size.x / 2, Size.y / 2, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    shot.Grid[g] = gc;
                }
            }
            shots.Add(shot);
        }
        // -videoListener 1: the sound as heard at the (first) video camera instead of at the participant's ears
        if (ListenAtCamera && shots.Count > 0)
        {
            foreach (var l in FindObjectsByType<AudioListener>()) l.enabled = false;
            shots[0].Cam.gameObject.AddComponent<AudioListener>();
        }
        Time.captureFramerate = Fps;
        _tex = new Texture2D(Size.x, Size.y, TextureFormat.RGB24, false);
        // the sound, rendered offline: each frame the audio of exactly 1/Fps s
        int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2, rate = AudioSettings.outputSampleRate;
        var samples = new List<float>();
        _audioStarted = Audio && rate > 0 && AudioRenderer.Start();
        int frames = Mathf.RoundToInt(Seconds * Fps);
        Debug.Log($"VideoExport: {shots.Count} cameras, {frames} frames at {Fps} fps, {Size.x}x{Size.y}");
        for (int f = 0; f < frames; f++)
        {
            yield return null;   // (WaitForEndOfFrame never comes in batch mode; the cameras render on demand)
            if (_audioStarted)
            {
                int n = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
                if (!_buffer.IsCreated || _buffer.Length != n) { if (_buffer.IsCreated) _buffer.Dispose(); _buffer = new Unity.Collections.NativeArray<float>(n, Unity.Collections.Allocator.Persistent); }
                if (n > 0 && AudioRenderer.Render(_buffer)) samples.AddRange(_buffer);
            }
            foreach (var s in shots)
            {
                var prev = RenderTexture.active;
                if (s.Grid != null)
                {
                    var people = Participants();
                    for (int g = 0; g < 4; g++)
                    {
                        var gc = s.Grid[g];
                        bool used = g < people.Count && people[g] != null;
                        if (used) Eye(people[g], gc.transform, s.Frames == 0);
                        gc.cullingMask = used ? ~0 : 0;   // (an unused quarter stays black)
                        gc.clearFlags = used ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                        gc.backgroundColor = Color.black;
                        gc.Render();
                        RenderTexture.active = gc.targetTexture;
                        // top left, top right, bottom left, bottom right (texture rows start at the bottom)
                        _tex.ReadPixels(new Rect(0, 0, Size.x / 2, Size.y / 2), (g % 2) * (Size.x / 2), g < 2 ? Size.y / 2 : 0, false);
                    }
                }
                else
                {
                    Place(s);
                    s.Cam.Render();
                    RenderTexture.active = s.Cam.targetTexture;
                    _tex.ReadPixels(new Rect(0, 0, Size.x, Size.y), 0, 0, false);
                }
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(s.Dir, $"frame_{f:D6}.jpg"), _tex.EncodeToJPG(92));
                s.Frames++;
            }
        }
        Time.captureFramerate = 0;
        if (_tex != null) { Destroy(_tex); _tex = null; }
        string wav = null;
        if (_audioStarted)
        {
            AudioRenderer.Stop();
            _audioStarted = false;
            if (_buffer.IsCreated) { _buffer.Dispose(); _buffer = default; }
            if (samples.Count > 0) { wav = Path.Combine(root, $"{NetworkingManager.LogPrefix}sound-{stamp}.wav"); WriteWav(wav, samples, channels, rate); }
            Debug.Log($"VideoExport: sound {samples.Count / Mathf.Max(1, channels) / (float)Mathf.Max(1, rate):F1} s at {rate} Hz, {channels} channels");
        }
        foreach (var s in shots)
        {
            var mp4 = s.Dir + ".mp4";
            if (Encode(s.Dir, mp4, wav))
            {
                Debug.Log($"VideoExport: {mp4} ({s.Frames} frames)");
                if (!KeepFrames) Directory.Delete(s.Dir, true);
            }
            else Debug.Log($"VideoExport: frames in {s.Dir} (ffmpeg not found; join them with: ffmpeg -framerate {Fps} -i frame_%06d.jpg -pix_fmt yuv420p {s.Name}.mp4)");
            Destroy(s.Cam.targetTexture); Destroy(s.Cam.gameObject);
            if (s.Grid != null) foreach (var gc in s.Grid) { Destroy(gc.targetTexture); Destroy(gc.gameObject); }
        }
        if (wav != null && !KeepFrames) File.Delete(wav);
        Busy = false;
        if (QuitWhenDone) Application.Quit();
    }

    static void WriteWav(string path, List<float> data, int channels, int rate)
    {
        using var w = new BinaryWriter(File.Create(path));
        int bytes = data.Count * 2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(rate); w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
        foreach (var x in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * 32767f), -32768, 32767));
    }

    Texture2D _tex;
    bool _audioStarted;
    Unity.Collections.NativeArray<float> _buffer;

    void Awake() => StartCoroutine(Start_());

    void OnDestroy()
    {
        if (Busy) Time.captureFramerate = 0;
        Busy = false;
        if (_audioStarted) { AudioRenderer.Stop(); _audioStarted = false; }
        if (_buffer.IsCreated) { _buffer.Dispose(); _buffer = default; }
        if (_tex != null) { Destroy(_tex); _tex = null; }
    }

    /// <summary>The participants: those of a replay, else every participant's avatar (the local one first).</summary>
    static List<PlayerAvatar> Participants()
    {
        if (ReplayPlayer.Active != null) return ReplayPlayer.Active.ReplayParticipants;
        var list = new List<PlayerAvatar>();
        var ps = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem : null;
        if (ps != null && ps.LocalPlayer != null) list.Add(ps.LocalPlayer);
        foreach (var a in FindObjectsByType<PlayerAvatar>()) if (!list.Contains(a) && a.GetComponent<AICar>() is var ai && (ai == null || !ai.enabled || a.cameras != null && a.cameras.Length > 0)) list.Add(a);
        return list;
    }

    /// <summary>A participant's own view: their camera (cars, two-wheelers, drones) or their head (pedestrians), level, smoothed.</summary>
    void Eye(PlayerAvatar p, Transform t, bool first)
    {
        var eye = p.cameras != null && p.cameras.Length > 0 && p.cameras[0] != null ? p.cameras[0].transform : null;
        var an = p.GetComponentInChildren<Animator>();
        var head = p.Type == AvatarType.Pedestrian && an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
        var pos = head != null ? head.position + p.transform.forward * 0.12f : eye != null ? eye.position
            : p.Type == AvatarType.Drone ? p.transform.position - Quaternion.Euler(0, p.transform.eulerAngles.y, 0) * Vector3.forward * 1.4f + Vector3.up * 0.45f   // a drone: from just behind
            : p.transform.position + Vector3.up * 1.6f;
        var rot = Quaternion.Euler(head != null ? 8f : 4f, p.transform.eulerAngles.y, 0f);
        if (first) t.SetPositionAndRotation(pos, rot);
        else { float k = 1f - Mathf.Exp(-14f / Fps); t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, 0.8f), Quaternion.Slerp(t.rotation, rot, k)); }
    }

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
        var ci = CultureInfo.InvariantCulture;
        if (s.Spec.StartsWith("rel:", StringComparison.OrdinalIgnoreCase) && me != null)
        {
            var r = s.Spec.Substring(4).Split(',');
            float G(int i, float d) => i < r.Length && float.TryParse(r[i], NumberStyles.Float, ci, out var x) ? x : d;
            var yaw = Quaternion.Euler(0, me.transform.eulerAngles.y, 0);
            var target = me.transform.position + yaw * new Vector3(G(0, 0), G(1, 1.6f), G(2, -5f));
            var rot = yaw * Quaternion.Euler(G(4, 10), G(3, 0), 0);
            if (s.Frames == 0) t.SetPositionAndRotation(target, rot);
            else { float k = 1f - Mathf.Exp(-10f / Fps); t.SetPositionAndRotation(Vector3.Lerp(t.position, target, k), Quaternion.Slerp(t.rotation, rot, k)); }
            s.Cam.fieldOfView = G(5, 60f);
            return;
        }
        var v = s.Spec.Split(',');
        float F(int i, float d) => i < v.Length && float.TryParse(v[i], NumberStyles.Float, ci, out var x) ? x : d;
        t.SetPositionAndRotation(new Vector3(F(0, 0), F(1, 1.6f), F(2, 0)), Quaternion.Euler(F(4, 0), F(3, 0), 0));
        s.Cam.fieldOfView = F(5, 60f);
    }

    bool Encode(string dir, string mp4, string wav)
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
                var p = Process.Start(new ProcessStartInfo(exe, $"-y -loglevel error -framerate {Fps} -i \"{Path.Combine(dir, "frame_%06d.jpg")}\" " + (wav != null ? $"-i \"{wav}\" -c:a aac -b:a 96k -shortest " : "") + $"-c:v libx264 -preset slow -pix_fmt yuv420p -crf {Crf} -movflags +faststart \"{mp4}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
                p.WaitForExit(600000);
                if (p.ExitCode == 0 && File.Exists(mp4)) return true;
            }
            catch (Exception) { }
        }
        return false;
    }
}
