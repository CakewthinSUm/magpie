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
    public bool Found { get; set; }
}

public sealed class GroupJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Members { get; set; } = [];
    public string? Routing { get; set; }
    public string? Affinity { get; set; }
    public string? Pick { get; set; }
    public bool Auto { get; set; }
    public bool Hidden { get; set; }
    public bool Disabled { get; set; }
    public bool Ready { get; set; }
    public List<MemberJson> MemberInfo { get; set; } = [];
    public string? Picked { get; set; }
    public bool Decides { get; set; }
    public string? Family { get; set; }
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
}

public sealed class ModelRefJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string? Icon { get; set; }
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
    public string? GithubTokenMask { get; set; }
    public string? GithubTokenFrom { get; set; }
    public string? Notice { get; set; }
    /// <summary>An agent beyond loopback that couldn't be set again with the
    /// sharing key when sharing changed (agent.Rekey).</summary>
    public string? Rekeyed { get; set; }
}
