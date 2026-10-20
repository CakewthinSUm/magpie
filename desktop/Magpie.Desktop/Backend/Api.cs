using System.Text.Json.Serialization;

namespace Magpie.Desktop.Backend;

// The shapes of the API's answers, page by page (internal/gui/*.go). Only
// what a page drawn here shows is here: a page adds its own as it is drawn.

// ---- /api/state (api.go, stateJSON) ----

public sealed class StateJson
{
    public List<AgentJson> Agents { get; set; } = [];
    public List<ProfileJson> Profiles { get; set; } = [];
    public string Catalog { get; set; } = "";
    public string? Notice { get; set; }
    public SettingsJson Settings { get; set; } = new();
    public int Served { get; set; }
    public List<RecoveredJson>? Recovered { get; set; }
    public bool? Last { get; set; }
}

public sealed class AgentJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Path { get; set; } = "";
    public List<FieldJson> Fields { get; set; } = [];
    public bool Wired { get; set; }
    public string? Source { get; set; }
    public int Stale { get; set; }
    public ModelCountJson? Models { get; set; }
    public bool Menu { get; set; }
    public bool CliMissing { get; set; }
    public bool Joined { get; set; }
    public bool Failover { get; set; }
}

public sealed class FieldJson
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public List<OptionJson> Options { get; set; } = [];
}

public sealed class OptionJson
{
    public string Value { get; set; } = "";
    public string? Label { get; set; }
    public string? Note { get; set; }
    public string? Icon { get; set; }
    public string? Group { get; set; }
}

public sealed class ModelCountJson
{
    public int Shown { get; set; }
    public int Listed { get; set; }
    public List<string>? Names { get; set; }
}

public sealed class ProfileJson
{
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public bool InUse { get; set; }
}

public sealed class SettingsJson
{
    public string Lang { get; set; } = "system";
    public string Theme { get; set; } = "system";
    public int TextSize { get; set; }
    public string? UIFont { get; set; }
    public string? CodeFont { get; set; }
}

public sealed class RecoveredJson
{
    public string File { get; set; } = "";
    public string Path { get; set; } = "";
    public string Why { get; set; } = "";
}

// ---- /api/providers (providers.go, providersJSON, providerJSON) ----

public sealed class ProvidersJson
{
    public List<ProviderJson> Providers { get; set; } = [];
    public List<PresetJson> Presets { get; set; } = [];
    public List<ExcludedJson> Excluded { get; set; } = [];
    public GatewayJson Gateway { get; set; } = new();
    public string? CodexDaemon { get; set; }
    public string? CodexApp { get; set; }
    public List<PluginSubJson> Plugins { get; set; } = [];
    public List<string>? OnPlugins { get; set; }
    public List<string>? Movable { get; set; }
    public string? FileError { get; set; }
    public bool Fetching { get; set; }
}

public sealed class ProviderJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Preset { get; set; } = "";
    public string Host { get; set; } = "";
    public string Chat { get; set; } = "";
    public string Responses { get; set; } = "";
    public string Anthropic { get; set; } = "";
    public string? Gemini { get; set; }
    public string Catalog { get; set; } = "";
    public string Website { get; set; } = "";
    public string KeysUrl { get; set; } = "";
    public bool Searches { get; set; }
    public bool Off { get; set; }
    public bool Ready { get; set; }
    public List<ModelJson> Models { get; set; } = [];
    public int Exposed { get; set; }
    public bool NoModels { get; set; }
    public bool Unlisted { get; set; }
    public List<ProviderAgent> Agents { get; set; } = [];
    public KeyState Key { get; set; } = new();
    public List<KeyInfo> KeyList { get; set; } = [];
    public AccountJson? Account { get; set; }
    public List<string> Fallback { get; set; } = [];
    public string Routing { get; set; } = "";
    public string Affinity { get; set; } = "";
    public DateTimeOffset? Fetched { get; set; }
    public string? ListError { get; set; }
    public MoveJson? Move { get; set; }
}

public sealed class KeyState
{
    public bool Set { get; set; }
    public string Masked { get; set; } = "";
    public bool Optional { get; set; }
}

public sealed class KeyInfo
{
    public string Id { get; set; } = "";
    public string Masked { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Off { get; set; }
}

public sealed class ModelJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    /// <summary>Exposed to agents (modelJSON's "on").</summary>
    public bool On { get; set; }
    /// <summary>Its context window before the user's, in tokens.</summary>
    public int Listed { get; set; }
    public int Context { get; set; }
    public bool Free { get; set; }
    public string? Desktop { get; set; }
}

public sealed class ProviderAgent
{
    public string Agent { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string AgentIcon { get; set; } = "";
}

public sealed class AccountJson
{
    public string User { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class MoveJson
{
    public string Package { get; set; } = "";
    public string State { get; set; } = "";
    public string? Error { get; set; }
}

public sealed class PresetJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Website { get; set; } = "";
    public string Group { get; set; } = "";
    public bool Added { get; set; }
}

public sealed class ExcludedJson
{
    public string Agent { get; set; } = "";
    public string User { get; set; } = "";
    public string Why { get; set; } = "";
}

public sealed class GatewayJson
{
    public string Url { get; set; } = "";
    public bool Lan { get; set; }
    public List<string> LanUrls { get; set; } = [];
    /// <summary>MAGPIE_PUBLIC_URL when it is one of the LAN addresses.</summary>
    public string PublicUrl { get; set; } = "";
    /// <summary>MAGPIE_ADDR puts it beyond loopback: every caller needs a key.</summary>
    public bool OnNetwork { get; set; }
    public bool Running { get; set; }
    /// <summary>This process serves it.</summary>
    public bool Mine { get; set; }
    public string Version { get; set; } = "";
    /// <summary>Another magpie serves it, and it is older than this one.</summary>
    public bool Older { get; set; }
    public int Models { get; set; }
    public List<GatewayCallJson> Calls { get; set; } = [];
}

/// <summary>gateway.Call as the Gateway page's recent calls list it, without
/// the bodies, which GET /api/gateway/call reads when a row is opened.</summary>
public sealed class GatewayCallJson
{
    public ulong Id { get; set; }
    public DateTimeOffset Time { get; set; }
    public string Agent { get; set; } = "";
    public string Model { get; set; } = "";
    public string Provider { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Status { get; set; }
    public long Ms { get; set; }
    public long Ttft { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>GET /api/gateway/call: one call's wire bodies.</summary>
public sealed class GatewayCallBodyJson
{
    public string RequestBody { get; set; } = "";
    public string ResponseBody { get; set; } = "";
    public bool RequestTruncated { get; set; }
    public bool ResponseTruncated { get; set; }
}

// ---- /api/caller-keys (caller_keys.go, keyRow, budget.Status) ----

public sealed class CallerKeysJson
{
    public List<CallerKeyJson> Keys { get; set; } = [];
    public Dictionary<string, string> AccountNames { get; set; } = [];
    /// <summary>The new key's secret, from add-key and rotate-key only.</summary>
    public string Secret { get; set; } = "";
}

public sealed class CallerKeyJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Off { get; set; }
    public string Masked { get; set; } = "";
    /// <summary>What it has used of its first limit; UsedAll has each.</summary>
    public KeyUsageJson? Used { get; set; }
    public List<KeyUsageJson> UsedAll { get; set; } = [];
}

/// <summary>budget.Status: what a key has used of one of its limits.</summary>
public sealed class KeyUsageJson
{
    public string Period { get; set; } = "";
    public int Days { get; set; }
    public int Hours { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset Reset { get; set; }
    public int Calls { get; set; }
    public long Tokens { get; set; }
    public double Cost { get; set; }
    public int InFlight { get; set; }
    public long TokenLimit { get; set; }
    public double CostLimit { get; set; }
    public long TokensLeft { get; set; }
    public double CostLeft { get; set; }
    /// <summary>The key is refused until Reset.</summary>
    public bool Spent { get; set; }
}

public sealed class PluginSubJson
{
    public string Id { get; set; } = "";
    public string Pid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Spec { get; set; } = "";
    public bool SignedIn { get; set; }
    public int Models { get; set; }
}

// ---- /api/usage and /api/usage/quotas (usageapi, provider) ----

public sealed class UsageJson : TotalsJson
{
    public string Period { get; set; } = "";
    public DateTimeOffset Since { get; set; }
    public string Bucket { get; set; } = "";
    public List<UsagePointJson> Series { get; set; } = [];
    public List<UsageGroupJson> Agents { get; set; } = [];
    public List<UsageGroupJson> Models { get; set; } = [];
    public List<UsageGroupJson> Accounts { get; set; } = [];
    public List<UsageGroupJson> CallerKeys { get; set; } = [];
    public int SessionCount { get; set; }
    public string Path { get; set; } = "";
}

public sealed class UsagePointJson : TotalsJson
{
    public string Label { get; set; } = "";
    public DateTimeOffset Time { get; set; }
}

public sealed class UsageGroupJson : TotalsJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Sub { get; set; }
    public string? Icon { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Agent { get; set; }
}

/// <summary>usage.Totals: a period's, a group's or a point's counts.</summary>
public class TotalsJson
{
    public int Calls { get; set; }
    public int Errors { get; set; }
    public int Input { get; set; }
    public int Output { get; set; }
    public int CacheRead { get; set; }
    public int CacheWrite { get; set; }
    public int Reasoning { get; set; }
    public double Cost { get; set; }
    public int Unpriced { get; set; }
}

public sealed class QuotaJson
{
    public string Provider { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string? Plan { get; set; }
    public string? User { get; set; }
    public string? Alias { get; set; }
    public string? Kind { get; set; }
    public List<QuotaWindowJson> Windows { get; set; } = [];
    public string? Balance { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? ResetsAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? Until { get; set; }
    /// <summary>Whether the plan renews when Until passes: "auto", "off", or
    /// "" when the vendor doesn't say which.</summary>
    public string? Renew { get; set; }
    public bool ReadDeferred { get; set; }
    public string? From { get; set; }
}

public sealed class QuotaWindowJson
{
    public string Name { get; set; } = "";
    public double Used { get; set; }
    public bool Unlimited { get; set; }
    public DateTimeOffset? ResetsAt { get; set; }
    public string? Display { get; set; }
    public double Amount { get; set; }
    public double Limit { get; set; }
    public string? Unit { get; set; }
}

// ---- /api/usage/requests (usageapi.Ledger) ----

public sealed class LedgerJson : TotalsJson
{
    public string Period { get; set; } = "";
    public List<LedgerRowJson> Rows { get; set; } = [];
    public int Offset { get; set; }
    public int Total { get; set; }
    public string Bucket { get; set; } = "";
    public string? Day { get; set; }
    public List<NamedJson> Agents { get; set; } = [];
    public List<NamedJson> Providers { get; set; } = [];
    public List<string> Purposes { get; set; } = [];
}

public sealed class LedgerRowJson
{
    public long RouteId { get; set; }
    public DateTimeOffset T { get; set; }
    public string Agent { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string Model { get; set; } = "";
    public string? Requested { get; set; }
    public string? Served { get; set; }
    public string? ProviderAccount { get; set; }
    public int Input { get; set; }
    public int Output { get; set; }
    public int CacheRead { get; set; }
    public int CacheWrite { get; set; }
    public double Cost { get; set; }
    public bool Priced { get; set; }
    public bool Swapped { get; set; }
    public int Status { get; set; }
    public string? Error { get; set; }
    public string? Purpose { get; set; }
    public string? Session { get; set; }
    public string? ComputerName { get; set; }
    public string? CallerKeyLabel { get; set; }
    public string? Kind { get; set; }
    public string? Access { get; set; }
}

public sealed class NamedJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
}

// ---- /api/groups (groupsJSON) ----

public sealed class GroupsJson
{
    public List<GroupJson> Groups { get; set; } = [];
    public List<ModelRefJson> Models { get; set; } = [];
    public List<PoolJson> Pools { get; set; } = [];
    public List<ModelRefJson> Deciders { get; set; } = [];
    public bool Found { get; set; }
    public List<TemplateJson> Templates { get; set; } = [];
    public string? Shared { get; set; }
}

public sealed class GroupJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Members { get; set; } = [];
    public List<string>? Match { get; set; }
    public List<string>? Matched { get; set; }
    public string? Routing { get; set; }
    public string? Affinity { get; set; }
    public string? Pick { get; set; }
    public bool Sink { get; set; }
    public bool Unlists { get; set; }
    public bool Auto { get; set; }
    public bool Hidden { get; set; }
    public bool Disabled { get; set; }
    public bool Ready { get; set; }
    public List<MemberJson> MemberInfo { get; set; } = [];
    /// <summary>The member a manual group sends every request to, as
    /// groupsState works it out (its Pick, else its first).</summary>
    public string? Picked { get; set; }
    public bool Decides { get; set; }
    public string? Family { get; set; }
    public List<string> Offers { get; set; } = [];
    public List<string> Shared { get; set; } = [];
    public List<PatternHitJson> Patterns { get; set; } = [];
    public ClassifierFailedJson? ClassifierFailed { get; set; }
}

/// <summary>A group's pattern and how many models it matches now: one that
/// matches nothing is said, not left an empty group.</summary>
public sealed class PatternHitJson
{
    public string Pattern { get; set; } = "";
    public int Models { get; set; }
}

/// <summary>The classifier's last failure, which the group's card says
/// (#1929): until it answers, no rule with an intent matches.</summary>
public sealed class ClassifierFailedJson
{
    public string Error { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class MemberJson
{
    public string Id { get; set; } = "";
    public bool Ready { get; set; }
    public string? Provider { get; set; }
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Model { get; set; }
    public string? Effort { get; set; }
    public bool ProviderOff { get; set; }
    /// <summary>The model's id without the effort the member is fixed at
    /// ("provider/model:low"); empty for one without.</summary>
    public string? Of { get; set; }
    public bool Group { get; set; }
    public bool Fast { get; set; }
    public int On { get; set; }
}

/// <summary>A provider with several keys or accounts on, which routes over
/// them already (the Routing page lists them under the groups).</summary>
public sealed class PoolJson
{
    public string Provider { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public string Kind { get; set; } = "";
    public List<string> Who { get; set; } = [];
    public string Routing { get; set; } = "";
    public string Affinity { get; set; } = "";
}

/// <summary>A group for a common need made of the user's own models, which
/// the page adds in a click (provider.Templates).</summary>
public sealed class TemplateJson
{
    public string Kind { get; set; } = "";
    public GroupJson Group { get; set; } = new();
    public string? Why { get; set; }
    public string? Need { get; set; }
    public string? Of { get; set; }
}

public sealed class ModelRefJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string? Icon { get; set; }
    /// <summary>An image model whose vendor picks the model itself,
    /// whichever is named (a ChatGPT account's), which Settings' Images
    /// row says (groups.go modelRef.VendorPicks).</summary>
    public bool VendorPicks { get; set; }
}

// ---- /api/gateway/trace (traceJSON, routeJSON) ----

public sealed class TraceJson
{
    public long Seq { get; set; }
    public List<RouteJson> Routes { get; set; } = [];
    public TraceTotalsJson Totals { get; set; } = new();
    public bool Mine { get; set; }
    public DateTimeOffset Now { get; set; }
}

public sealed class TraceTotalsJson
{
    public int Requests { get; set; }
    public int Rerouted { get; set; }
    public int Errors { get; set; }
}

public sealed class RouteJson
{
    public long Seq { get; set; }
    public long Id { get; set; }
    public DateTimeOffset Time { get; set; }
    public string Agent { get; set; } = "";
    public string? Session { get; set; }
    public string? Kind { get; set; }
    public string Model { get; set; } = "";
    public string? Effort { get; set; }
    public string Provider { get; set; } = "";
    public bool Done { get; set; }
    public int Status { get; set; }
    public string? Error { get; set; }
    public long Ms { get; set; }
    public int Tokens { get; set; }
    public int Out { get; set; }
    public long Ttft { get; set; }
    public string? Served { get; set; }
    public bool Swapped { get; set; }
    public bool Routed { get; set; }
    public string? SessionTitle { get; set; }
    public double Cost { get; set; }
    public bool Priced { get; set; }
    public int Unpriced { get; set; }
    public string? Folder { get; set; }
    /// <summary>The routing group the agent asked for, when it asked for
    /// one.</summary>
    public GroupRefJson? Group { get; set; }
    /// <summary>The accounts and keys in the order routing weighed them,
    /// first first, then each try in the order it was made.</summary>
    public List<WeighedJson> Order { get; set; } = [];
    public List<TryJson> Tries { get; set; } = [];
}

/// <summary>The routing group a request asked for, as the trace recorded
/// it (gateway.GroupRef).</summary>
public sealed class GroupRefJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Routing { get; set; } = "";
    public string Affinity { get; set; } = "";
    public bool Auto { get; set; }
    public List<string> Members { get; set; } = [];
    public List<SubGroupJson> Subs { get; set; } = [];
    public List<PausedJson> Paused { get; set; } = [];
}

public sealed class SubGroupJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Routing { get; set; } = "";
    public string In { get; set; } = "";
}

/// <summary>A model a pause rule left out of a request.</summary>
public sealed class PausedJson
{
    public string Member { get; set; } = "";
    public string? Group { get; set; }
    public int Rule { get; set; }
    public string? Use { get; set; }
    public List<string> When { get; set; } = [];
}

/// <summary>One account or key as routing weighed it.</summary>
public sealed class WeighedJson
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    /// <summary>The account, or the key's name or its masked self.</summary>
    public string? Who { get; set; }
    /// <summary>"account", "key", or "provider" when it has one.</summary>
    public string Kind { get; set; } = "";
    public string Model { get; set; } = "";
    public string? Fixed { get; set; }
    public bool Fast { get; set; }
    public bool Fallback { get; set; }
    public bool Shared { get; set; }
    public double Used { get; set; }
    public RestJson? Rest { get; set; }
}

/// <summary>How long a failed account or key sits out, and by what.</summary>
public sealed class RestJson
{
    public string Why { get; set; } = "";
    public int Status { get; set; }
    public DateTimeOffset Until { get; set; }
    public string By { get; set; } = "";
    public int Failures { get; set; }
}

/// <summary>One candidate trying the request.</summary>
public sealed class TryJson
{
    public string Id { get; set; } = "";
    public string? Model { get; set; }
    public string? Effort { get; set; }
    public string? Fixed { get; set; }
    public DateTimeOffset Start { get; set; }
    public bool Done { get; set; }
    public int Status { get; set; }
    public long Ms { get; set; }
    public long Ttft { get; set; }
    public long FirstText { get; set; }
    public string? Served { get; set; }
    public bool Swapped { get; set; }
    public bool Routed { get; set; }
    /// <summary>Why it failed, as rest tells it (rate, credit, quota…).</summary>
    public string? Fail { get; set; }
    /// <summary>What the vendor said, word for word.</summary>
    public string? Error { get; set; }
    public RestJson? Rest { get; set; }
    /// <summary>ms waited before it was tried again, the last one left.</summary>
    public long Again { get; set; }
    /// <summary>The agent canceled the request in the pause before the
    /// retry Again was to be waited (#1894).</summary>
    public bool Gone { get; set; }
}

// ---- /api/sessions (sessionsJSON, sessionJSON) ----

public sealed class SessionsJson
{
    public List<SessionJson> Sessions { get; set; } = [];
    public bool Terminal { get; set; }
    public List<string> Dirs { get; set; } = [];
}

public sealed class SessionJson
{
    public string Agent { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Cwd { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Nickname { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset Last { get; set; }
    public int Input { get; set; }
    public int Output { get; set; }
    [JsonPropertyName("cache_read")]
    public int CacheRead { get; set; }
    public double Cost { get; set; }
    public int Unpriced { get; set; }
    public string Resume { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Gateway { get; set; }
    [JsonPropertyName("read_only")]
    public bool ReadOnly { get; set; }
    public bool Transcript { get; set; }
    public string? Wsl { get; set; }

    // ---- the same session as /api/sessions/manage lists it (sessions.Managed) ----

    /// <summary>What it spent, model by model.</summary>
    public List<SessionModelJson> Models { get; set; } = [];
    /// <summary>Its files' size in bytes.</summary>
    public long Size { get; set; }
    public int Messages { get; set; }
    public int Files { get; set; }
    /// <summary>Whether magpie can move it to its trash.</summary>
    public bool Deletable { get; set; }
    /// <summary>Codex: the provider it was made with, and the one Codex
    /// lists in its history now.</summary>
    public string? Provider { get; set; }
    [JsonPropertyName("uses_provider")]
    public string? UsesProvider { get; set; }
    /// <summary>Where magpie's gateway sent its calls, the most first.</summary>
    public List<SessionViaJson> Via { get; set; } = [];
}

/// <summary>One model's share of a session (sessions.Model).</summary>
public sealed class SessionModelJson
{
    public string Model { get; set; } = "";
    public int Input { get; set; }
    public int Output { get; set; }
    [JsonPropertyName("cache_read")]
    public int CacheRead { get; set; }
    [JsonPropertyName("cache_write")]
    public int CacheWrite { get; set; }
    public double Cost { get; set; }
    public bool Priced { get; set; }
}

/// <summary>One place a session's calls went through the gateway (usage.Via).</summary>
public sealed class SessionViaJson
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string? Effort { get; set; }
    public int Calls { get; set; }
    public int Tokens { get; set; }
}

/// <summary>/api/sessions/manage (sessions_manage.go): one agent's sessions
/// with what deleting needs, the agents to pick from, and the trash.</summary>
public sealed class SessionsManageJson
{
    public List<ManageAgentJson> Agents { get; set; } = [];
    public string Agent { get; set; } = "";
    public List<SessionJson> Sessions { get; set; } = [];
    public bool Terminal { get; set; }
    public List<TrashedJson> Trash { get; set; } = [];
    public string TrashDir { get; set; } = "";
    public bool Recording { get; set; }
    public bool Recorded { get; set; }
}

public sealed class ManageAgentJson
{
    public string Agent { get; set; } = "";
    public int Count { get; set; }
    public bool Deletable { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
}

/// <summary>A session in magpie's trash (sessions.Trashed).</summary>
public sealed class TrashedJson
{
    public string Key { get; set; } = "";
    public string Agent { get; set; } = "";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Cwd { get; set; } = "";
    public DateTimeOffset Last { get; set; }
    public DateTimeOffset Deleted { get; set; }
    public long Size { get; set; }
    public bool Gateway { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<TrashedItemJson> Items { get; set; } = [];
}

public sealed class TrashedItemJson
{
    public string From { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>What POST /api/sessions/delete moved, and what it refused.</summary>
public sealed class SessionsDeleteJson
{
    public List<string> Deleted { get; set; } = [];
    public List<SessionRefusedJson> Refused { get; set; } = [];
}

public sealed class SessionRefusedJson
{
    public string Id { get; set; } = "";
    public string Error { get; set; } = "";
    /// <summary>Still being written: its agent may be running it.</summary>
    public bool Active { get; set; }
}

// ---- /api/library (libraryJSON, library.View) ----

public sealed class LibraryJson
{
    public List<LibraryAgentJson> Agents { get; set; } = [];
    public List<LibraryServerJson> Servers { get; set; } = [];
    public List<LibrarySkillJson> Skills { get; set; } = [];
    public List<FoundSkillJson> FoundSkills { get; set; } = [];
    public InstructionsJson? Instructions { get; set; }
    public string Home { get; set; } = "";
    public string Dir { get; set; } = "";
    public bool Partial { get; set; }
    /// <summary>What the last change couldn't write into an agent (library.Problem).</summary>
    public List<LibraryProblemJson>? Problems { get; set; }
    /// <summary>What the last change did, as its own answer has it.</summary>
    public LibraryResultJson? Result { get; set; }
}

/// <summary>One thing that couldn't be given to an agent: "instructions",
/// "mcp:&lt;name&gt;" or "skill:&lt;name&gt;".</summary>
public sealed class LibraryProblemJson
{
    public string Agent { get; set; } = "";
    public string What { get; set; } = "";
    public string Error { get; set; } = "";
    public bool Own { get; set; }
}

/// <summary>What a change wrote, and what it couldn't.</summary>
public sealed class LibraryResultJson
{
    public List<string> Changed { get; set; } = [];
    public List<LibraryProblemJson> Problems { get; set; } = [];
    public string? Backup { get; set; }
}

/// <summary>The answer to a change of the instructions alone
/// (instructionsJSON): the instructions, not the whole library.</summary>
public sealed class InstructionsAnswerJson
{
    public string Part { get; set; } = "";
    public InstructionsJson? Instructions { get; set; }
    public LibraryResultJson? Result { get; set; }
    public List<LibraryProblemJson>? Problems { get; set; }
}

public sealed class LibraryAgentJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string? Note { get; set; }
    public bool Gone { get; set; }
}

public sealed class LibraryServerJson
{
    public string Name { get; set; } = "";
    public string Transport { get; set; } = "";
    public string? Command { get; set; }
    public List<string>? Args { get; set; }
    public string? Url { get; set; }
    public List<string> Agents { get; set; } = [];
    public string? Icon { get; set; }
    /// <summary>The agents it couldn't be given to, and why (library.ServerView).</summary>
    public Dictionary<string, string>? Problems { get; set; }
    /// <summary>magpie's sign-in to a streamable HTTP server.</summary>
    public McpSignInJson? SignIn { get; set; }
}

public sealed class McpSignInJson
{
    public bool SignedIn { get; set; }
    public bool Dead { get; set; }
}

public sealed class LibrarySkillJson
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Source { get; set; }
    public string Kind { get; set; } = "";
    public List<string> Agents { get; set; } = [];
    public bool Missing { get; set; }
    /// <summary>The agents it couldn't be given to, and why (library.SkillView).</summary>
    public Dictionary<string, string>? Problems { get; set; }
}

public sealed class FoundSkillJson
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Agents { get; set; } = [];
    /// <summary>Agents with another skill by that name, which stays as it is.</summary>
    public List<string>? Others { get; set; }
    /// <summary>Agents with a copy of the very same files.</summary>
    public List<string>? Copies { get; set; }
}

public sealed class InstructionsJson
{
    public string Shared { get; set; } = "";
    public List<InstructionSetJson> Sets { get; set; } = [];
    public List<AgentInstructionsJson> Agents { get; set; } = [];
}

public sealed class AgentInstructionsJson
{
    public string Agent { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Path { get; set; } = "";
    public bool On { get; set; }
    /// <summary>What this agent gets besides the shared text.</summary>
    public string Extra { get; set; } = "";
    /// <summary>Lines of the user's own in the file.</summary>
    public int Own { get; set; }
    /// <summary>magpie's part was changed in the file itself.</summary>
    public bool Edited { get; set; }
    /// <summary>A file the agent reads instead.</summary>
    public string Override { get; set; } = "";
    /// <summary>The file is the user's profile to the agent.</summary>
    public bool Profile { get; set; }
}

public sealed class InstructionSetJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Active { get; set; }
    /// <summary>The user's own file it is kept in, when it is one.</summary>
    public string File { get; set; } = "";
}

// ---- /api/plugins (pluginsJSON, pluginEntryJSON) ----

public sealed class PluginsJson
{
    public List<PluginEntryJson> Plugins { get; set; } = [];
    public bool Bun { get; set; }
    public string BunVersion { get; set; } = "";
    public string? Error { get; set; }
    public bool Picker { get; set; }
    public bool Mirror { get; set; }
    /// <summary>The built-ins with accounts a plugin could run (provider.MoveCandidate).</summary>
    public List<MoveCandidateJson> Movable { get; set; } = [];
}

/// <summary>A deprecated built-in's accounts, which a plugin can run instead.</summary>
public sealed class MoveCandidateJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Package { get; set; } = "";
    public int Accounts { get; set; }
    public string? Error { get; set; }
}

public sealed class PluginEntryJson
{
    public string Spec { get; set; } = "";
    public bool Off { get; set; }
    public string? Error { get; set; }
    public List<string> Providers { get; set; } = [];
    public string? Version { get; set; }
    public string? Latest { get; set; }
    public string? Icon { get; set; }
    public List<string> Moved { get; set; } = [];
    public bool Named { get; set; }
    public bool IsAgent { get; set; }
    public bool InMagpieOnly { get; set; }
    public PluginAgentJson? Agent { get; set; }
    public PluginPageJson? Page { get; set; }
    /// <summary>A git plugin's installed package and commit, and the commit its
    /// repository was at when last asked: an update is out when the two differ.</summary>
    public string? Package { get; set; }
    public string? Commit { get; set; }
    public string? Head { get; set; }
    /// <summary>Whether it is gateway middleware, and whether that is all it is.</summary>
    public bool IsMiddleware { get; set; }
    public bool MiddlewareOnly { get; set; }
    public bool IsGateway { get; set; }
    public bool GatewayOnly { get; set; }
    /// <summary>The update magpie made to it by itself lately.</summary>
    public PluginUpdatedJson? AutoUpdated { get; set; }
}

/// <summary>plugin.Updated: an update magpie made to a plugin by itself.</summary>
public sealed class PluginUpdatedJson
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

// ---- /api/plugins/builtin (builtinJSON, builtins.go) ----

public sealed class BuiltinJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>What it is, in a sentence, as its row says it.</summary>
    public string Sub { get; set; } = "";
    /// <summary>Whether it shows: its switch, or what uses it, makes it so.</summary>
    public bool On { get; set; }
    /// <summary>The user's own choice, before what uses it.</summary>
    public bool Chosen { get; set; }
    public string State { get; set; } = "";
    public string? Why { get; set; }
    /// <summary>How many things in magpie use it; one in use stays on.</summary>
    public int InUse { get; set; }
    public string? Use { get; set; }
    /// <summary>plugins.yml's error, when it can't be read: no switch can move.</summary>
    public string? Broken { get; set; }
}

// ---- /api/plugins/check (plugins.go, plugin.CheckNow) ----

public sealed class PluginCheckJson
{
    public DateTimeOffset At { get; set; }
    public PluginsJson? State { get; set; }
}

// ---- /api/plugins/market and /api/plugins/search (plugins.go, market.go) ----

public sealed class PluginMarketJson
{
    public List<PluginListingJson> Listings { get; set; } = [];
    public PluginsJson State { get; set; } = new();
}

public sealed class PluginListingJson
{
    public string Package { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public List<string> Providers { get; set; } = [];
    public bool Community { get; set; }
    public string? Replaces { get; set; }
    /// <summary>"middleware", "agent", "tools" or "page"; none for a provider.</summary>
    public string? Kind { get; set; }
    public Dictionary<string, string>? Summary { get; set; }
    public PluginNpmJson? Npm { get; set; }
}

/// <summary>What npm says of a package now.</summary>
public sealed class PluginNpmJson
{
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Publisher { get; set; }
    public string? Homepage { get; set; }
    public string? Repository { get; set; }
    public int Weekly { get; set; }
    public string? Icon { get; set; }
}

public sealed class PluginSearchJson
{
    public List<PluginHitJson> Hits { get; set; } = [];
}

/// <summary>plugin.Hit: a package npm's search found, and what npm says of it.</summary>
public sealed class PluginHitJson
{
    public string Package { get; set; } = "";
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Publisher { get; set; }
    public string? Homepage { get; set; }
    public string? Repository { get; set; }
    public int Weekly { get; set; }
    public string? Icon { get; set; }
}

public sealed class PluginAgentJson
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Error { get; set; }
}

public sealed class PluginPageJson
{
    public string? Title { get; set; }
    public string? Replaces { get; set; }
}

// ---- /api/settings (settingsJSON's own fields; settings.Settings flattens) ----

public sealed class SettingsFullJson
{
    public string Version { get; set; } = "";
    public string Dir { get; set; } = "";
    public string Gateway { get; set; } = "";
    public string Lang { get; set; } = "system";
    public string Theme { get; set; } = "system";
    public int TextSize { get; set; }
    public bool Colorblind { get; set; }
    public bool Lan { get; set; }
    public string? PublicUrl { get; set; }
    public bool Portable { get; set; }
    public bool Wsl { get; set; }
    public bool Login { get; set; }
    public bool Web { get; set; }
    public string ProxyNow { get; set; } = "";
    public string ProxySource { get; set; } = "";
    public List<string>? LanUrls { get; set; }
    public bool LanContainer { get; set; }
    public string? GithubTokenMask { get; set; }
    public string? GithubTokenFrom { get; set; }
    public string? Notice { get; set; }
    /// <summary>An agent beyond loopback that couldn't be set again with the
    /// sharing key when sharing changed (agent.Rekey).</summary>
    public string? Rekeyed { get; set; }
    /// <summary>What clicking the tray icon opens: "panel" or "window".</summary>
    public string Tray { get; set; } = "panel";
    /// <summary>Whether a newer magpie is looked for, and downloaded, by itself.</summary>
    public bool NoAutoUpdate { get; set; }
    /// <summary>How often it looks for one, in minutes (settings.UpdateEveries).</summary>
    public int UpdateEvery { get; set; }
    /// <summary>Whether a newer version found is downloaded at once.</summary>
    public bool NoAutoDownload { get; set; }
    /// <summary>Whether a downloaded update is restarted into by itself.</summary>
    public bool AutoRestart { get; set; }
    /// <summary>Whether the header's Update pill shows.</summary>
    public bool NoUpdatePill { get; set; }
    public string? UpdateMirror { get; set; }
    /// <summary>The version the Update pill was hidden for.</summary>
    public string? UpdateSkip { get; set; }
    /// <summary>The built-in plugins, each with its switch (builtins.go).</summary>
    public List<BuiltinJson> Builtins { get; set; } = [];
    public bool GatewayOn { get; set; }
    public string? GatewayWhy { get; set; }
    /// <summary>Settings' own gateway mode: "on", "off", or "" for automatic.</summary>
    public string? GatewayMode { get; set; }
    /// <summary>MAGPIE_NO_ACCOUNTS set, which keeps account discovery off for this run.</summary>
    public bool NoAccountsEnv { get; set; }
    /// <summary>Whether agents' sign-ins are read and served as subscriptions.</summary>
    public bool NoAccountDiscovery { get; set; }

    // ---- General's other choices, sent whole by the snapshot save so a
    // desktop save never resets a value the web page set ----

    public bool Dock { get; set; }
    public bool DockWindow { get; set; }
    public bool Lightweight { get; set; }
    public bool KeepAwake { get; set; }
    public bool KeepAwakeDisplay { get; set; }
    public bool NoWSLAgents { get; set; }
    public string SessionTerminal { get; set; } = "";

    // ---- Allowances / usage ----

    public bool QuotaLeft { get; set; }
    public string QuotaReads { get; set; } = "";
    public string Currency { get; set; } = "usd";
    public bool ChineseUnits { get; set; }
    public bool PlainNames { get; set; }
    public bool PlainOwnNames { get; set; }
    public bool CodexAgentsV1 { get; set; }
    public bool FullContext { get; set; }
    public int CompactAt { get; set; }
    public bool CodexAsyncCompact { get; set; }
    public int CodexAsyncCompactPercent { get; set; }
    public List<string>? TrayUsages { get; set; }
    public string TrayUsage { get; set; } = "";
    public int TrayUsageEvery { get; set; }
    public bool TrayNoLogos { get; set; }
    public bool TrayNoBird { get; set; }
    public bool TrayPace { get; set; }
    public bool TrayNoCost { get; set; }
    public int UsageAlert { get; set; }
    public double BalanceAlert { get; set; }
    public int ResetReminder { get; set; }
    public int TurnDoneAlert { get; set; }
    public string? NotifyProblem { get; set; }
    public string CodexWarmup { get; set; } = "";
    public string ClaudeWarmup { get; set; } = "";
    public string CodexWarmAt { get; set; } = "";
    public string ClaudeWarmAt { get; set; } = "";
    public List<string>? CodexWarmAts { get; set; }
    public List<string>? ClaudeWarmAts { get; set; }
    public Dictionary<string, string>? CodexWarmAtOf { get; set; }
    public List<string>? CodexUsers { get; set; }
    public List<string>? CodexAutoReset { get; set; }
    public Dictionary<string, string>? CodexAutoResetOnly { get; set; }
    public List<string>? CodexNoCredits { get; set; }
    public DateTimeOffset? CodexWarmed { get; set; }
    public DateTimeOffset? ClaudeWarmed { get; set; }
    public bool WorkBuddy { get; set; }
    public bool Trae { get; set; }
    public bool MiniMax { get; set; }
    public bool Qoder { get; set; }
    public bool WorkBuddyCheckin { get; set; }
    public bool TraeCheckin { get; set; }
    public bool MiniMaxCheckin { get; set; }
    public bool QoderCheckin { get; set; }
    public List<WorkBuddyCheckinJson>? WorkBuddyCheckins { get; set; }
    public List<WorkBuddyCheckinJson>? TraeCheckins { get; set; }
    public List<WorkBuddyCheckinJson>? MiniMaxCheckins { get; set; }
    public List<WorkBuddyCheckinJson>? QoderCheckins { get; set; }
    public List<CheckinAccountJson>? CheckinAccounts { get; set; }
    public List<PluginCheckinJson>? CheckinPlugins { get; set; }
    public string CodexTitles { get; set; } = "";
    public string CodexDescriptions { get; set; } = "";
    public string CodexPredictions { get; set; } = "";
    public string CodexAutoReview { get; set; } = "";
    public List<ModelRefJson> TitleModels { get; set; } = [];
    public FxJson Fx { get; set; } = new();

    // ---- Network and sharing ----

    public string Proxy { get; set; } = "";
    public int Port { get; set; }
    public string? AddrEnv { get; set; }
    public bool BareModelIDs { get; set; }
    public List<string>? AllowIPs { get; set; }
    public string? AllowIPsEnv { get; set; }
    public List<string>? AllowIPsFromEnv { get; set; }
    public bool OnNetwork { get; set; }
    public List<string>? CorsOrigins { get; set; }

    // ---- Models ----

    public string Vision { get; set; } = "";
    public string VisionEffort { get; set; } = "";
    public string? VisionAuto { get; set; }
    public List<ModelRefJson> VisionModels { get; set; } = [];
    public string? VisionMissing { get; set; }
    public List<string>? VisionEfforts { get; set; }
    public string? VisionEffortNow { get; set; }
    public string ImageGen { get; set; } = "";
    public string? ImageGenAuto { get; set; }
    public string? ImageGenMissing { get; set; }
    public string? ImageGenShown { get; set; }
    public List<ModelRefJson> ImageGenModels { get; set; } = [];
    public string Searcher { get; set; } = "";
    public string SearcherEffort { get; set; } = "";
    public string? SearchAuto { get; set; }
    public string? SearchUnused { get; set; }
    public List<string>? SearchEfforts { get; set; }
    public string? SearchEffortNow { get; set; }
    public List<SearchChoiceJson> SearchChoices { get; set; } = [];
    public string? SearchProvider { get; set; }
    public List<string>? SearchRelays { get; set; }
    public SearchFailedJson? SearchFailed { get; set; }
    public List<string>? SearchLeftOut { get; set; }
    public List<SearchApiJson> SearchAPIs { get; set; } = [];
    public List<SearchVendorJson> SearchVendors { get; set; } = [];
    public string SearchFirst { get; set; } = "";
    public bool MemberModel { get; set; }
    public bool NoLoopGuard { get; set; }
    public int? RefusalRetries { get; set; }
    public bool ClaudeLeanPrompt { get; set; }
    public List<string>? DisabledModels { get; set; }
    public Dictionary<string, List<string>>? DisabledHits { get; set; }

    // ---- Privacy ----

    public bool Redact { get; set; }
    public bool RedactPersonal { get; set; }
    public Dictionary<string, bool>? RedactKinds { get; set; }
    public List<string>? RedactWords { get; set; }
    public List<RedactRuleJson>? RedactRules { get; set; }
    public bool NoStats { get; set; }
    public bool NoUsageStats { get; set; }

    // ---- Observability ----

    public OTelJson Otel { get; set; } = new();
    public bool OtelEnv { get; set; }
}

/// <summary>A USD→CNY rate as the Settings page shows it (api.go fxJSON):
/// the number a cost is multiplied by, when it was last learned, and
/// whether that is stale.</summary>
public sealed class FxJson
{
    public double Rate { get; set; }
    public DateTimeOffset? At { get; set; }
    public bool Stale { get; set; }
}

/// <summary>The OTLP/HTTP metadata export (settings.OTel): its endpoint
/// (the base URL), the headers sent with every signal, and what goes out.</summary>
public sealed class OTelJson
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = [];
    public bool Metrics { get; set; }
    public bool Sessions { get; set; }
    public bool Bodies { get; set; }
    public bool BodiesWhole { get; set; }
}

/// <summary>A masking rule the user added for a secret magpie's own rules
/// don't know (redact.Rule): its placeholder, and the prefix its values
/// start with or a pattern for them.</summary>
public sealed class RedactRuleJson
{
    public string Kind { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Regex { get; set; } = "";
}

/// <summary>A web search API as the Settings page shows it (api.go searchAPIJSON).</summary>
public sealed class SearchApiJson
{
    public string Vendor { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Key { get; set; }
    public string? Url { get; set; }
    public bool Ready { get; set; }
    public int Keys { get; set; }
    public string? Pick { get; set; }
    public bool Quota { get; set; }
    public bool Off { get; set; }
}

/// <summary>A vendor whose search API can be added (api.go searchVendorJSON).</summary>
public sealed class SearchVendorJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? KeysUrl { get; set; }
    public bool NeedUrl { get; set; }
    public bool KeyOptional { get; set; }
    public string? Base { get; set; }
}

/// <summary>A provider that can search for a model that can't (api.go searchChoiceJSON).</summary>
public sealed class SearchChoiceJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public string Small { get; set; } = "";
    public string? SmallName { get; set; }
    public List<ModelRefJson> Models { get; set; } = [];
    public bool Service { get; set; }
    public bool Own { get; set; }
}

/// <summary>The last search the named searcher didn't make (api.go searchFailedJSON).</summary>
public sealed class SearchFailedJson
{
    public string Searcher { get; set; } = "";
    public string? Why { get; set; }
    public bool NothingFound { get; set; }
    public bool NotSearched { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>One account a vendor's daily check-in reaches (provider.CheckinAccount).</summary>
public sealed class CheckinAccountJson
{
    public string Key { get; set; } = "";
    public string By { get; set; } = "";
    public string User { get; set; } = "";
    public bool On { get; set; }
}

/// <summary>A plugin's provider that checks in (provider.PluginCheckin).</summary>
public sealed class PluginCheckinJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool On { get; set; }
    public List<WorkBuddyCheckinJson>? Checkins { get; set; }
}

/// <summary>One account's last daily check-in (provider.WorkBuddyCheckin).</summary>
public sealed class WorkBuddyCheckinJson
{
    public string? User { get; set; }
    public string Day { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public string Outcome { get; set; } = "";
    public double Credit { get; set; }
    public int Streak { get; set; }
    public string? Msg { get; set; }
    public DateTimeOffset Until { get; set; }
    public bool Offline { get; set; }
    public int Tries { get; set; }
    public string? By { get; set; }
    public string? Vendor { get; set; }
}

/// <summary>What POST /api/proxy/check found (proxycheck.go proxyCheck):
/// the proxy in force, and the request through it and the one without.</summary>
public sealed class ProxyCheckJson
{
    public string? Proxy { get; set; }
    public string Source { get; set; } = "";
    public ProxyLegJson? Through { get; set; }
    public ProxyLegJson Direct { get; set; } = new();
}

/// <summary>One leg of a proxy check: the address it left from, and whether
/// the connection went to the proxy (proxycheck.go proxyLeg).</summary>
public sealed class ProxyLegJson
{
    public string? Ip { get; set; }
    public string? Loc { get; set; }
    public long Ms { get; set; }
    public string? Error { get; set; }
    public bool Proxied { get; set; }
}

/// <summary>POST /api/settings/port's answer (api.go:2036): the settings and
/// what moving the gateway port did.</summary>
public sealed class PortAnswerJson
{
    public SettingsFullJson Settings { get; set; } = new();
    public PortResultJson Port { get; set; } = new();
}

/// <summary>What changing the gateway's port did (port.go).</summary>
public sealed class PortResultJson
{
    public int Port { get; set; }
    public List<string>? Moved { get; set; }
    public string? Error { get; set; }
}

// ---- /api/davsync (davsync.View, davsync.go) ----

/// <summary>Sync's setup and how the last sync went (davsync.View), as the
/// Settings page's Sync and backup part shows it.</summary>
public sealed class DavSyncViewJson
{
    public bool On { get; set; }
    public string? Url { get; set; }
    public string? User { get; set; }
    public bool PasswordSet { get; set; }
    public bool PassphraseSet { get; set; }
    public bool Keys { get; set; }
    public bool Agents { get; set; }
    /// <summary>"webdav", "s3" or "github", when on.</summary>
    public string? Kind { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public bool PathStyle { get; set; }
    public string? Branch { get; set; }
    public bool Library { get; set; }
    public bool Usage { get; set; }
    public string? UsageError { get; set; }
    public DateTimeOffset Last { get; set; }
    public string? Error { get; set; }
    public NoticeJson? Notice { get; set; }
    public OtherViewJson? Other { get; set; }
    public Dictionary<string, OtherViewJson>? Servers { get; set; }
    /// <summary>Minutes between syncs by itself; 0 is never.</summary>
    public int Auto { get; set; }
    /// <summary>Whether the last Restore can be undone.</summary>
    public bool Undo { get; set; }
    public ServerFileJson? ServerFile { get; set; }
    /// <summary>Subscriptions whose settings sync brought with no account
    /// here: sync carries no sign-in.</summary>
    public List<string>? SignIn { get; set; }
    /// <summary>What a restore brought in (POST /api/davsync/restore alone).</summary>
    public List<string>? Brought { get; set; }
}

/// <summary>The other kind's server, kept for moving sync back
/// (davsync.OtherView).</summary>
public sealed class OtherViewJson
{
    public string Kind { get; set; } = "";
    public string Url { get; set; } = "";
    public string? User { get; set; }
    public bool PasswordSet { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public bool PathStyle { get; set; }
    public string? Branch { get; set; }
}

/// <summary>What a sync replaced when a part changed on both sides, or when
/// this computer first joined a folder with a setup in it (davsync.Notice).</summary>
public sealed class NoticeJson
{
    public DateTimeOffset At { get; set; }
    public List<string>? Here { get; set; }
    public List<string>? There { get; set; }
    public bool Saved { get; set; }
    public bool Restored { get; set; }
}

/// <summary>What the server held where the backup should be, when it isn't
/// one (davsync.ServerFile).</summary>
public sealed class ServerFileJson
{
    public string What { get; set; } = "";
    public int Size { get; set; }
    public string? Title { get; set; }
    public string? Format { get; set; }
    public string? Type { get; set; }
    public string? Moved { get; set; }
    public bool Replace { get; set; }
}

/// <summary>POST /api/backup/export's answer.</summary>
public sealed class ExportAnswerJson
{
    public string Path { get; set; } = "";
    public int Providers { get; set; }
    public int Profiles { get; set; }
    public int Agents { get; set; }
    public bool Library { get; set; }
}

/// <summary>backup.Result as POST /api/backup/import answers it: its fields
/// have no JSON tags, so they go out in their Go names (PascalCase).</summary>
public sealed class ImportResultJson
{
    [System.Text.Json.Serialization.JsonPropertyName("Added")] public int Added { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Replaced")] public int Replaced { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("NeedKey")] public List<string>? NeedKey { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("SignIn")] public List<string>? SignIn { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Plugins")] public List<string>? Plugins { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PluginErrors")] public Dictionary<string, string>? PluginErrors { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Settings")] public bool Settings { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Profiles")] public int Profiles { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Agents")] public int Agents { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Skipped")] public List<string>? Skipped { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Library")] public bool Library { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("LibraryProblems")] public List<LibraryProblemJson>? LibraryProblems { get; set; }
}

// ---- /api/update (updateJSON, update.go) ----

public sealed class UpdateJson
{
    /// <summary>"checking", "found", "downloading", "ready", "available",
    /// "latest", "error", "blocked", or "" before it was ever asked.</summary>
    public string State { get; set; } = "";
    public string Current { get; set; } = "";
    public string? Latest { get; set; }
    public string? Notes { get; set; }
    public string? Url { get; set; }
    public string? Stuck { get; set; }
    public string? Error { get; set; }
    public long Done { get; set; }
    public long Total { get; set; }
    public long Size { get; set; }
    public bool Retry { get; set; }
    public string? Mirror { get; set; }
    public bool Waiting { get; set; }
    public bool GaveUp { get; set; }
}
