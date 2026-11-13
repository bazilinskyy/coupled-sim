using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Asks Claude (Anthropic Messages API) a short question, e.g. a virtual pedestrian's CROSS / WAIT
/// decision from a SceneDescriber description. Raw HTTP with UnityWebRequest (no extra packages in
/// the Unity project). The API key comes from the environment variable ANTHROPIC_API_KEY (never
/// stored in the project); without it nothing is sent. Options: -llmModel (default claude-opus-5-5),
/// -llmEffort (default low, for quick decisions), -llmEndpoint (default https://api.anthropic.com,
/// e.g. a local test server). Using it sends the scene description to Anthropic and costs money.
/// </summary>
public static class LlmClient
{
    public static string Model => Arg("-llmModel") ?? "claude-opus-5-5";
    public static string Effort => Arg("-llmEffort") ?? "low";
    public static string Endpoint => (Arg("-llmEndpoint") ?? "https://api.anthropic.com").TrimEnd('/');
    public static string ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    public static bool Available => !string.IsNullOrEmpty(ApiKey);

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    public static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s ?? "")
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: if (c < 0x20) sb.Append($"\\u{(int)c:x4}"); else sb.Append(c); break;
            }
        return sb.ToString();
    }

    [Serializable] class Block { public string type; public string text; }
    [Serializable] class Response { public Block[] content; public string stop_reason; }

    /// <summary>Sends one question; calls <paramref name="done"/> with the text answer, or null on failure, refusal or no key.</summary>
    public static IEnumerator Ask(string system, string user, Action<string> done, int maxTokens = 1024)
    {
        if (!Available) { Debug.LogWarning("LlmClient: ANTHROPIC_API_KEY not set: no request sent"); done(null); yield break; }
        // thinking is always on for this model (adaptive); low effort keeps decisions quick.
        // "fallbacks": "default" retries a refused request on a suitable model (server-side).
        var body = "{" +
            $"\"model\":\"{Escape(Model)}\",\"max_tokens\":{maxTokens}," +
            $"\"output_config\":{{\"effort\":\"{Escape(Effort)}\"}}," +
            "\"fallbacks\":\"default\"," +
            $"\"system\":\"{Escape(system)}\"," +
            $"\"messages\":[{{\"role\":\"user\",\"content\":\"{Escape(user)}\"}}]" + "}";
        using var req = new UnityWebRequest(Endpoint + "/v1/messages", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("content-type", "application/json");
        req.SetRequestHeader("x-api-key", ApiKey);
        req.SetRequestHeader("anthropic-version", "2026-10-21");
        req.SetRequestHeader("anthropic-beta", "server-side-fallback-2026-07-01");
        req.timeout = 60;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            // 429 / 5xx are worth retrying later; 4xx means a wrong request or key
            Debug.LogWarning($"LlmClient: HTTP {req.responseCode} {req.error}: {req.downloadHandler.text}");
            done(null); yield break;
        }
        Response r = null;
        try { r = JsonUtility.FromJson<Response>(req.downloadHandler.text); } catch (Exception e) { Debug.LogWarning("LlmClient: bad response " + e.Message); }
        if (r == null || r.stop_reason == "refusal" || r.content == null) { done(null); yield break; }
        var text = new StringBuilder();
        foreach (var b in r.content) if (b != null && b.type == "text" && b.text != null) text.Append(b.text);   // skip thinking blocks
        done(text.ToString().Trim());
    }
}
