using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using CalculusModel;

namespace CalculusLink.Bridge;

/// <summary>Snapshot returned by the bridge's <c>GET /status</c>: whether it
/// serves, where, and the editors that hold a model (one per desktop window
/// or paired browser tab).</summary>
public class BridgeStatus
{
    public bool Running { get; set; }
    public int Port { get; set; }
    public List<BridgeEditor> Editors { get; set; } = [];
}

/// <summary>One open model as the bridge lists it. <see cref="Id"/> is what a
/// call names with <c>?editor=</c>.</summary>
public class BridgeEditor
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Kind { get; set; }
    public bool Dirty { get; set; }
    public bool ReadOnly { get; set; }

    public override string ToString() => string.IsNullOrEmpty(Name) ? Id : Name!;
}

/// <summary>Raised when the bridge returns a domain (operation) error.</summary>
public class BridgeException(string message) : Exception(message);

/// <summary>
/// A thin HTTP client for the Calculus bridge — the server the Calculus desktop
/// app runs on 127.0.0.1:7421 for agents and integrations. Every model operation goes through the
/// plain-REST <c>POST /api/&lt;operation&gt;</c> dispatcher (the path is the operation
/// name, the body is its payload, the result comes back directly). This is the
/// surface meant for programmatic clients; the bridge's <c>/mcp</c> endpoint is the
/// equivalent JSON-RPC surface for MCP/LLM hosts and isn't used from here.
/// <c>GET /status</c> drives the connection check. The bridge is loopback-only;
/// a token is needed only when the app's "Require a token from agents" is on
/// (default port 7421; GET http://127.0.0.1:7421/ describes it).
/// </summary>
public class CalculusBridgeClient(int port = 7421, string? token = null)
{
    public const int DefaultPort = 7421;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public int Port { get; } = port;
    /// <summary>
    /// The agent token the Calculus desktop app requires when "Require a token
    /// from agents" is on (shown in its Agents popover, kept in its bridge.json).
    /// Null/empty when the switch is off.
    /// </summary>
    public string? Token { get; } = token?.Trim() is { Length: > 0 } t ? t : null;
    /// <summary>
    /// The editor every operation goes to (its id from <see cref="BridgeStatus.Editors"/>).
    /// Null lets the bridge pick — fine with one model open, refused as
    /// <c>ambiguous</c> with several.
    /// </summary>
    public string? Editor { get; set; }
    private string BaseUrl => $"http://127.0.0.1:{Port}";

    private HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}{path}") { Content = content };
        if (Token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        return request;
    }

    /// <summary>
    /// Does the app accept this client's token? <c>/status</c> answers anyone, so
    /// it cannot tell; <c>/api/editors</c> sits behind the agent-token check like
    /// every operation does. False only on 401 — any other failure is left to
    /// the status probe and the calls themselves.
    /// </summary>
    public async Task<bool> IsAuthorizedAsync()
    {
        try
        {
            var response = await Http.SendAsync(Request(HttpMethod.Get, "/api/editors"));
            return response.StatusCode != System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            return true;
        }
    }

    public async Task<BridgeStatus?> GetStatusAsync()
    {
        try
        {
            var response = await Http.SendAsync(Request(HttpMethod.Get, "/status"));
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<BridgeStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Invoke a hub operation by name (<c>POST /api/&lt;action&gt;</c>) with the given
    /// payload and return the result JSON. On a hub error the body is
    /// <c>{ "error": ... }</c> with a 4xx/5xx status, surfaced as a <see cref="BridgeException"/>.
    /// </summary>
    public async Task<JsonNode> CallAsync(string action, JsonObject payload)
    {
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var query = Editor is null ? string.Empty : $"?editor={Uri.EscapeDataString(Editor)}";
        var response = await Http.SendAsync(Request(HttpMethod.Post, $"/api/{action}{query}", content));
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = $"Hub returned {(int)response.StatusCode}";
            try
            {
                if (JsonNode.Parse(text)?["error"] is { } error) message = error.ToJsonString();
            }
            catch { /* keep the status-code message */ }
            throw new BridgeException(message);
        }

        return JsonNode.Parse(text) ?? new JsonObject();
    }

    // --- Typed helpers -------------------------------------------------------

    /// <summary>addObjects — ids supplied on the objects are preserved by the editor.</summary>
    public async Task AddObjectsAsync(IReadOnlyList<ModelObject> objects)
    {
        if (objects.Count == 0) return;
        await CallAsync("addObjects", ObjectsPayload(objects));
    }

    /// <summary>updateObjects — replace existing objects matched by id.</summary>
    public async Task UpdateObjectsAsync(IReadOnlyList<ModelObject> objects)
    {
        if (objects.Count == 0) return;
        await CallAsync("updateObjects", ObjectsPayload(objects));
    }

    /// <summary>
    /// setColorRules — replace the colour-by-rule entries whose name starts with
    /// <paramref name="scope"/> (placed first, so they win); an empty list clears
    /// the scope. The user's own rules stay.
    /// </summary>
    public async Task SetColorRulesAsync(string scope, IReadOnlyList<(string name, string query, string color)> rules)
    {
        var array = new JsonArray();
        foreach (var (name, query, color) in rules)
        {
            array.Add(new JsonObject { ["name"] = name, ["query"] = query, ["color"] = color, ["active"] = true });
        }
        await CallAsync("setColorRules", new JsonObject { ["rules"] = array, ["scope"] = scope });
    }

    /// <summary>setStructuralRepresentation — returns the representation it replaced.</summary>
    public async Task<string?> SetStructuralRepresentationAsync(string representation)
    {
        var result = await CallAsync("setStructuralRepresentation", new JsonObject { ["representation"] = representation });
        return result["previous"]?.GetValue<string>();
    }

    /// <summary>removeObjects — the editor drops dependents that cannot exist without them.</summary>
    public async Task RemoveObjectsAsync(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0) return;
        var idArray = new JsonArray();
        foreach (var id in ids) idArray.Add(id);
        await CallAsync("removeObjects", new JsonObject { ["ids"] = idArray });
    }

    public async Task<List<ModelObject>> GetAllObjectsAsync(string? type = null)
    {
        var payload = new JsonObject();
        if (type is not null) payload["type"] = type;
        var result = await CallAsync("getAllObjects", payload);
        return ObjectsFromResult(result);
    }

    /// <summary>getSelection with full objects.</summary>
    public async Task<List<ModelObject>> GetSelectionAsync()
    {
        var result = await CallAsync("getSelection", new JsonObject { ["full"] = true });
        return ObjectsFromResult(result);
    }

    /// <summary>getRelatedObjects — pull in objects the given ids reference (e.g. a bolt's parts).</summary>
    public async Task<List<ModelObject>> GetReferencedObjectsAsync(IEnumerable<string> ids)
    {
        var idArray = new JsonArray();
        foreach (var id in ids) idArray.Add(id);
        if (idArray.Count == 0) return [];
        var result = await CallAsync("getRelatedObjects", new JsonObject
        {
            ["ids"] = idArray,
            ["direction"] = "references",
        });
        return ObjectsFromResult(result);
    }

    /// <summary>
    /// patchObjects — set the full <c>userdata</c> dict on an object. The hub merges
    /// <c>properties</c> shallowly, so the whole userdata map must be sent.
    /// </summary>
    public async Task PatchUserdataAsync(IEnumerable<(string id, Dictionary<string, string> userdata)> patches)
    {
        var patchArray = new JsonArray();
        foreach (var (id, userdata) in patches)
        {
            var ud = new JsonObject();
            foreach (var kv in userdata) ud[kv.Key] = kv.Value;
            patchArray.Add(new JsonObject
            {
                ["id"] = id,
                ["properties"] = new JsonObject { ["userdata"] = ud },
            });
        }
        if (patchArray.Count == 0) return;
        await CallAsync("patchObjects", new JsonObject { ["patches"] = patchArray });
    }

    private static JsonObject ObjectsPayload(IReadOnlyList<ModelObject> objects)
    {
        var arrayJson = CalculusModelSerializer.SerializeObjects(objects);
        return new JsonObject { ["objects"] = JsonNode.Parse(arrayJson) };
    }

    private static List<ModelObject> ObjectsFromResult(JsonNode result)
    {
        var objects = result["objects"];
        return objects is null
            ? []
            : CalculusModelSerializer.DeserializeObjects(objects.ToJsonString());
    }
}
