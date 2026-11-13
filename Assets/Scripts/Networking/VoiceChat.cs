using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Voice between participants (e.g. passengers of a shared AV talking to each other): each agent's
/// microphone (16 kHz mono) is sent to the others over the simulator's network in 20 ms packets
/// (through the host) and played from the speaker's avatar as 3D sound. Each agent records its own
/// microphone to ExperimentLogs/Voice-{prefix}{date}.wav (16-bit PCM; the file name has the start
/// time, and Voice-*.csv lists the UTC time of every packet for synchronisation).
/// Turn it on with <c>-voice</c> (open microphone) or <c>-voice ptt</c> (push to talk: hold V);
/// <c>-voiceDevice name</c> picks the microphone; <c>-voiceRecordOnly</c> records without sending.
/// </summary>
public class VoiceChat : MonoBehaviour
{
    public const int Rate = 16000;
    public const int FrameSamples = Rate / 50;   // 20 ms
    public KeyCode PushToTalkKey = KeyCode.V;

    public static VoiceChat Instance { get; private set; }
    public static bool Enabled => Instance != null;

    bool _ptt, _recordOnly;
    string _device;
    AudioClip _mic;
    int _readPos;
    readonly List<float> _pending = new List<float>();
    int _seq;
    Action<VoiceMsg> _send;
    Func<int> _localPlayer;
    BinaryWriter _wav;
    StreamWriter _times;
    long _wavSamples;
    readonly Dictionary<int, Speaker> _speakers = new Dictionary<int, Speaker>();

    /// <summary>Starts voice when the command line asks for it; <paramref name="send"/> sends one packet to the others.</summary>
    public static void Setup(Action<VoiceMsg> send, Func<int> localPlayer)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-voice");
        if (i < 0) return;
        if (Instance == null)
        {
            var go = new GameObject("Voice chat");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VoiceChat>();
        }
        Instance._ptt = i + 1 < args.Length && args[i + 1].ToLowerInvariant() == "ptt";
        Instance._recordOnly = Array.IndexOf(args, "-voiceRecordOnly") >= 0;
        int d = Array.IndexOf(args, "-voiceDevice");
        Instance._device = d >= 0 && d + 1 < args.Length ? args[d + 1] : null;
        Instance._send = send;
        Instance._localPlayer = localPlayer;
    }

    void Start()
    {
        if (Microphone.devices.Length == 0) { Debug.LogWarning("VoiceChat: no microphone; only receiving"); return; }
        _mic = Microphone.Start(_device, true, 2, Rate);
        Debug.Log($"VoiceChat: microphone '{_device ?? Microphone.devices[0]}' at {Rate} Hz, {(_ptt ? "push to talk (" + PushToTalkKey + ")" : "open")}{(_recordOnly ? ", recording only" : "")}");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_mic != null) Microphone.End(_device);
        CloseWav();
        foreach (var s in _speakers.Values) s.Dispose();
    }

    void OnApplicationQuit() => CloseWav();

    void Update()
    {
        if (_mic == null) return;
        int pos = Microphone.GetPosition(_device);
        if (pos < 0) return;
        int n = pos - _readPos;
        if (n < 0) n += _mic.samples;
        if (n <= 0) return;
        var buf = new float[n];
        // read across the wrap of the ring buffer
        int first = Mathf.Min(n, _mic.samples - _readPos);
        var a = new float[first];
        _mic.GetData(a, _readPos);
        Array.Copy(a, buf, first);
        if (n > first) { var b = new float[n - first]; _mic.GetData(b, 0); Array.Copy(b, 0, buf, first, n - first); }
        _readPos = pos;
        Record(buf);
        bool talking = !_ptt || Input.GetKey(PushToTalkKey);
        if (!talking || _recordOnly || _send == null) { _pending.Clear(); return; }
        _pending.AddRange(buf);
        while (_pending.Count >= FrameSamples)
        {
            var msg = new VoiceMsg { Player = _localPlayer != null ? _localPlayer() : 0, Seq = _seq++, Packed = Pack(_pending, FrameSamples) };
            _pending.RemoveRange(0, FrameSamples);
            try { _send(msg); } catch (Exception e) { Debug.LogWarning("VoiceChat: send " + e.Message); _send = null; }
        }
    }

    // two 16-bit samples per int
    static List<int> Pack(List<float> s, int count)
    {
        var l = new List<int>(count / 2);
        for (int i = 0; i + 1 < count; i += 2)
        {
            int lo = (ushort)(short)Mathf.Clamp(Mathf.RoundToInt(s[i] * 32767f), -32768, 32767);
            int hi = (ushort)(short)Mathf.Clamp(Mathf.RoundToInt(s[i + 1] * 32767f), -32768, 32767);
            l.Add(lo | (hi << 16));
        }
        return l;
    }

    static float[] Unpack(List<int> l)
    {
        var s = new float[l.Count * 2];
        for (int i = 0; i < l.Count; i++)
        {
            s[2 * i] = (short)(l[i] & 0xffff) / 32768f;
            s[2 * i + 1] = (short)((l[i] >> 16) & 0xffff) / 32768f;
        }
        return s;
    }

    /// <summary>A packet from another agent (the host relays clients' packets to everyone).</summary>
    public static void Receive(VoiceMsg msg)
    {
        if (Instance == null || msg.Packed == null) return;
        if (Instance._localPlayer != null && msg.Player == Instance._localPlayer()) return;
        if (!Instance._speakers.TryGetValue(msg.Player, out var sp)) Instance._speakers[msg.Player] = sp = new Speaker(msg.Player);
        sp.Push(Unpack(msg.Packed));
    }

    void LateUpdate()
    {
        foreach (var s in _speakers.Values) s.Follow();
    }

    // ------------------------------------------------------------------ recording

    void Record(float[] buf)
    {
        try
        {
            if (_wav == null)
            {
                Directory.CreateDirectory("ExperimentLogs");
                var stamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
                _wav = new BinaryWriter(File.Create($"ExperimentLogs/Voice-{NetworkingManager.LogPrefix}{stamp}.wav"));
                _wav.Write(new byte[44]);   // header written on close
                _times = new StreamWriter($"ExperimentLogs/Voice-{NetworkingManager.LogPrefix}{stamp}.csv") { AutoFlush = false };
                _times.WriteLine("utc;time;first_sample;samples");
            }
            _times.WriteLine($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff};{Time.time:F3};{_wavSamples};{buf.Length}");
            foreach (var v in buf) _wav.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            _wavSamples += buf.Length;
        }
        catch (Exception e) { Debug.LogWarning("VoiceChat: recording " + e.Message); _wav = null; }
    }

    void CloseWav()
    {
        if (_wav == null) return;
        try
        {
            var w = _wav; _wav = null;
            int bytes = (int)(_wavSamples * 2);
            w.Seek(0, SeekOrigin.Begin);
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + bytes);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(bytes);
            w.Dispose();
            _times?.Dispose();
        }
        catch (Exception e) { Debug.LogWarning("VoiceChat: closing recording " + e.Message); }
    }

    /// <summary>Plays one remote participant's voice from their avatar's head.</summary>
    sealed class Speaker
    {
        readonly int _player;
        readonly Queue<float> _buffer = new Queue<float>();
        readonly object _lock = new object();
        readonly AudioSource _source;
        bool _started;

        public Speaker(int player)
        {
            _player = player;
            var go = new GameObject("Voice of player " + player);
            DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.spatialBlend = 1f; _source.minDistance = 1f; _source.maxDistance = 30f; _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.clip = AudioClip.Create("voice " + player, Rate * 2, 1, Rate, true, Read);
            _source.loop = true;
        }

        public void Push(float[] s)
        {
            lock (_lock)
            {
                foreach (var v in s) _buffer.Enqueue(v);
                // keep the delay short: drop the oldest beyond 0.4 s
                while (_buffer.Count > Rate * 4 / 10) _buffer.Dequeue();
            }
            // start once 60 ms are buffered (jitter)
            if (!_started && _buffer.Count >= FrameSamples * 3) { _source.Play(); _started = true; }
        }

        void Read(float[] data)
        {
            lock (_lock)
                for (int i = 0; i < data.Length; i++) data[i] = _buffer.Count > 0 ? _buffer.Dequeue() : 0f;
        }

        public void Follow()
        {
            var ps = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem : null;
            var avatar = ps != null ? ps.GetAvatar(_player) : null;
            if (avatar == null) return;
            var cam = avatar.GetComponentInChildren<Camera>(true);
            _source.transform.position = cam != null ? cam.transform.position : avatar.transform.position + Vector3.up * 1.6f;
        }

        public void Dispose() { if (_source != null) Destroy(_source.gameObject); }
    }
}

public struct VoiceMsg : INetMessage
{
    public int Player;
    public int Seq;
    public List<int> Packed;
    public bool FromHost;
    public int MessageId => FromHost ? (int)MsgId.S_Voice : (int)MsgId.C_Voice;
    public void Sync<T>(T s) where T : ISynchronizer
    {
        s.Sync(ref Player);
        s.Sync(ref Seq);
        s.Sync(ref Packed);
    }
}
