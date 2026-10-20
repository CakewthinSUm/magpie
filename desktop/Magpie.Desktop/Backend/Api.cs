using System.Text.Json.Serialization;

namespace Magpie.Desktop.Backend;

// The shapes of the API's answers, page by page (internal/gui/*.go). Only
// what a page drawn here shows is here: a page adds its own as it is drawn.

// ---- /api/state (api.go, stateJSON) ----

public class StateJson
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
    public List<StaleCopyJson>? StaleCopies { get; set; }
    public ModelCountJson? Models { get; set; }
    public bool Menu { get; set; }
    public bool CliMissing { get; set; }
    public bool Joined { get; set; }
    public bool Failover { get; set; }
    /// <summary>Its config no longer does what magpie set (agent.Drift).</summary>
    public DriftJson? Drift { get; set; }
    /// <summary>An app that takes magpie by a link of its own (Cindy).</summary>
    public string? Import { get; set; }
    /// <summary>Whether that app has magpie already; null when it can't be read.</summary>
    public bool? Added { get; set; }
    /// <summary>The command that starts it on magpie, for one that takes the
    /// gateway only from its environment (agy).</summary>
    public string? Launch { get; set; }
    /// <summary>Its own list doesn't name magpie's models.</summary>
    public bool NoNames { get; set; }
    public NativeStateJson? Native { get; set; }
}

/// <summary>agent.Drift: what is off in an agent's config, and how to set it
/// again.</summary>
public sealed class DriftJson
{
    /// <summary>"unwired", "replaced", "bypassed", "unreachable" or "proxied".</summary>
    public string Kind { get; set; } = "";
    public string Field { get; set; } = "";
    public string? Now { get; set; }
    public string Want { get; set; } = "";
    public string Detail { get; set; } = "";
    public string? Addr { get; set; }
    /// <summary>The address WSL reaches Windows at now, which reapplying
    /// points it at.</summary>
    public string? Move { get; set; }
}

/// <summary>agent.StaleCopy: a copy of a connected agent still running on the
/// model list it started with.</summary>
public sealed class StaleCopyJson
{
    /// <summary>"app", "ide", "daemon", "helper", "embedded" or "cli".</summary>
    public string Kind { get; set; } = "";
    public string? App { get; set; }
    public DateTimeOffset Since { get; set; }
}

/// <summary>agent.NativeState: an agent magpie talks to rather than writes a
/// config for (Aside), and what it says of each field.</summary>
public sealed class NativeStateJson
{
    public string Provider { get; set; } = "";
    public string? Detail { get; set; }
    public string Runtime { get; set; } = "";
    public Dictionary<string, NativeFieldJson> Fields { get; set; } = [];
}

public sealed class NativeFieldJson
{
    public string Value { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Detail { get; set; }
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
    public List<string>? Icons { get; set; }
    public string? Group { get; set; }
    public string? GroupIcon { get; set; }
    /// <summary>The catalog model, the same in every agent.</summary>
    public string? Ref { get; set; }
    /// <summary>Costs its subscription nothing.</summary>
    public bool Free { get; set; }
    public double Rate { get; set; }
    public double RateWas { get; set; }
    /// <summary>The tokens the model takes, when known.</summary>
    public int Context { get; set; }
    /// <summary>On an effort field's default, the level the agent takes then.</summary>
    public string? Takes { get; set; }
    /// <summary>Who the agent asks for this model itself, magpie not in the
    /// way ("Anthropic").</summary>
    public string? Direct { get; set; }
    public bool Via { get; set; }
    /// <summary>Magpie serves it on the account the agent is signed in to
    /// itself, so the picker folds these into one row.</summary>
    public bool Same { get; set; }
    /// <summary>The value of the option a dated id is another name of.</summary>
    public string? Alias { get; set; }
    /// <summary>The agent whose requests a fast pick sends it on.</summary>
    public string? FastFor { get; set; }
    public bool Fast { get; set; }
    public bool UltraOk { get; set; }
    public bool Ultra { get; set; }
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
    /// <summary>What it holds, by agent, to be read before it is applied.</summary>
    public List<ProfileGroupJson> Agents { get; set; } = [];
    /// <summary>What it gives out from the library.</summary>
    public ProfileLibraryJson? Library { get; set; }
}

public sealed class ProfileGroupJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public List<ProfileItemJson> Fields { get; set; } = [];
    public List<string>? Servers { get; set; }
    public List<string>? Skills { get; set; }
    public bool Instructions { get; set; }
    public ProfileModelsJson? Models { get; set; }
}

public sealed class ProfileItemJson
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Follows { get; set; }
    /// <summary>A value that looked like a key or a token: never shown.</summary>
    public bool Hidden { get; set; }
}

public sealed class ProfileModelsJson
{
    public int Hidden { get; set; }
    public bool Only { get; set; }
    public int Picked { get; set; }
}

public sealed class ProfileLibraryJson
{
    public int Servers { get; set; }
    public int Skills { get; set; }
    public bool Instructions { get; set; }
}

/// <summary>The answer to POST /api/profile/{action}: the whole state, plus
/// what applying one changed.</summary>
public sealed class ProfileAnswerJson : StateJson
{
    public int Changed { get; set; }
    public LibraryResultJson? Library { get; set; }
}

// ---- /api/agents/cli and /api/agents/install ----

public sealed class CliAnswerJson
{
    public Dictionary<string, CliJson> Agents { get; set; } = [];
    /// <summary>Some versions are still being asked for: ask again.</summary>
    public bool Pending { get; set; }
}

/// <summary>agent.CLI: an agent's command-line program, and the newest one.</summary>
public sealed class CliJson
{
    public string? Version { get; set; }
    public string? Latest { get; set; }
    /// <summary>What updates it: npm, bun, pnpm, brew, self; "" when magpie
    /// can't tell.</summary>
    public string? Via { get; set; }
    public string? Command { get; set; }
    public bool Update { get; set; }
    /// <summary>The agent's desktop app, installed beside it.</summary>
    public string? App { get; set; }
    public bool Admin { get; set; }
    public string? Path { get; set; }
    /// <summary>Found in a folder magpie looks in that a terminal doesn't.</summary>
    public bool OffPath { get; set; }
}

/// <summary>agent.Install: an agent not on this machine, and how to install it.</summary>
public sealed class InstallJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public List<InstallCmdJson> Commands { get; set; } = [];
    /// <summary>Its settings are here but its CLI isn't.</summary>
    public bool Missing { get; set; }
}

public sealed class InstallCmdJson
{
    public string Via { get; set; } = "";
    public string Command { get; set; } = "";
    public string? Node { get; set; }
    public string? Admin { get; set; }
}

// ---- /api/agent-models/{id} (agent_models.go) ----

public sealed class AgentModelsJson
{
    public List<AgentModelJson> Models { get; set; } = [];
    public bool Ordered { get; set; }
    /// <summary>Shown only the models picked for it.</summary>
    public bool Only { get; set; }
    /// <summary>Its pickers offer models of its own beside magpie's.</summary>
    public bool Own { get; set; }
    public bool OwnHidden { get; set; }
}

public sealed class AgentModelJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Its provider's name, or "Routing groups".</summary>
    public string Group { get; set; } = "";
    public string? Icon { get; set; }
    public List<string>? Icons { get; set; }
    public string? Logo { get; set; }
    public int Context { get; set; }
    public bool Hidden { get; set; }
    /// <summary>The agent is set to it, so it can't be taken out.</summary>
    public bool InUse { get; set; }
    /// <summary>One of the agent's own, a ChatGPT account's.</summary>
    public bool Own { get; set; }
}

public sealed class SettingsJson
{
    public string Lang { get; set; } = "system";
    public string Theme { get; set; } = "system";
    public int TextSize { get; set; }
    public string? UIFont { get; set; }
    public string? CodeFont { get; set; }
    /// <summary>How the agents are listed, by agent id (settings.AgentOrder).</summary>
    public List<string>? AgentOrder { get; set; }
    public List<string>? AgentsHidden { get; set; }
    public List<string>? AgentsShown { get; set; }
}

public sealed class RecoveredJson
{
    public string File { get; set; } = "";
    public string Path { get; set; } = "";
    public string Why { get; set; } = "";
}

/// <summary>GET /api/agents/preview/{id}: what disconnecting would change in
/// an agent's files, line by line (agent.FileChange).</summary>
public sealed class PreviewJson
{
    public List<FileChangeJson>? Changes { get; set; }
    /// <summary>What the preview couldn't read, when it couldn't.</summary>
    public string? Error { get; set; }
    /// <summary>Aside's plan, which an offline disconnect names.</summary>
    public string? Revision { get; set; }
}

public sealed class FileChangeJson
{
    /// <summary>As the user knows it, ~ for home.</summary>
    public string Path { get; set; } = "";
    public bool Removed { get; set; }
    public bool Created { get; set; }
    public List<LineDiffJson>? Lines { get; set; }
}

/// <summary>One line a file changes: "-" taken out, "+" put in, "~" a line
/// whose value goes back to another (Was is the line now).</summary>
public sealed class LineDiffJson
{
    public string Op { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Was { get; set; }
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
    /// <summary>The plugin each deprecated built-in goes to, by its id.</summary>
    public Dictionary<string, string>? MovesTo { get; set; }
    /// <summary>Retired built-ins whose accounts haven't moved to their
    /// plugins yet, served by nothing until they do.</summary>
    public List<MoveCandidateJson>? Unmoved { get; set; }
    public string? FileError { get; set; }
    public bool Fetching { get; set; }
}

/// <summary>provider.MoveCandidate: a deprecated built-in's accounts, which
/// its plugin runs.</summary>
public sealed class MoveCandidateJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Package { get; set; } = "";
    public int Accounts { get; set; }
    public string? Error { get; set; }
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
    /// <summary>The decision API's base, a routing group's classifier.</summary>
    public string? Decide { get; set; }
    /// <summary>The API the user gave its Base URL as ("chat", "responses",
    /// "anthropic", "gemini", "decide").</summary>
    public string? BaseApi { get; set; }
    public string Catalog { get; set; } = "";
    public string Website { get; set; } = "";
    public string KeysUrl { get; set; } = "";
    public bool Searches { get; set; }
    public bool Off { get; set; }
    public bool Ready { get; set; }
    public List<ModelJson> Models { get; set; } = [];
    public int Exposed { get; set; }
    /// <summary>How many of its models draw images.</summary>
    public int Draws { get; set; }
    public List<string>? DrawIds { get; set; }
    public bool NoModels { get; set; }
    public bool Unlisted { get; set; }
    public List<ProviderAgent> Agents { get; set; } = [];
    public KeyState Key { get; set; } = new();
    public List<KeyInfo> KeyList { get; set; } = [];
    public AccountJson? Account { get; set; }
    public List<string> Fallback { get; set; } = [];
    public string Routing { get; set; } = "";
    public string Affinity { get; set; } = "";
    public bool Sink { get; set; }
    public bool KeepLogin { get; set; }
    public string? KeepLoginAs { get; set; }
    public bool SwitchOnly { get; set; }
    public bool Sponsored { get; set; }
    public DateTimeOffset? Fetched { get; set; }
    public string? ListError { get; set; }
    public MoveJson? Move { get; set; }
    // what its editor sets beside them, carried whole so a save never drops
    // one it doesn't show. MaxConcurrency is a number or null (null follows
    // what its plugin says); the rest are numbers the API omits when 0.
    public int? MaxConcurrency { get; set; }
    public int QueueLimit { get; set; }
    public int QueueWait { get; set; }
    public int MaxRpm { get; set; }
    public double PriceRate { get; set; }
    public Dictionary<string, int>? Contexts { get; set; }
    public Dictionary<string, int>? Outputs { get; set; }
    public Dictionary<string, int>? Compacts { get; set; }
    public string Proxy { get; set; } = "";
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? AccountProxies { get; set; }
    public Dictionary<string, string>? AccountNames { get; set; }
    public Dictionary<string, int>? AccountConcurrency { get; set; }
    public Dictionary<string, List<string>>? AccountModels { get; set; }
    public bool PinUpstream { get; set; }
    public bool Unredacted { get; set; }
    public bool InsecureTls { get; set; }
    public bool TakesInsecureTls { get; set; }
    public bool NativeCompaction { get; set; }
    public bool CarriesCompaction { get; set; }
    public bool PassUserAgent { get; set; }
    public bool OwnClient { get; set; }
    public string AuthHeader { get; set; } = "";
    public bool BothAuth { get; set; }
    public string? BalanceUrl { get; set; }
    public string? BalancePath { get; set; }
    public string? BalanceMethod { get; set; }
    public string? ModelsUrl { get; set; }
    /// <summary>An account-wide balance token: whether the vendor takes one,
    /// and whether one is saved; never the token itself.</summary>
    public BalanceTokenJson? BalanceToken { get; set; }
    public string? Catalog_ { get; set; }
    public string? BalanceFix { get; set; }
    public string? ModelTest { get; set; }
    public Dictionary<string, string>? TestsAs { get; set; }
    public bool DecideTest { get; set; }
    public bool MarksKinds { get; set; }
    public bool Cline { get; set; }
    public bool Vertex { get; set; }
    public VertexJson? VertexInfo { get; set; }
    public ZhipuTeamJson? ZhipuTeam { get; set; }
    public AccessKeyJson? AccessKey { get; set; }
    public string? Endpoint { get; set; }
    public List<string>? Speaks { get; set; }
    public string? AnonKey { get; set; }
    /// <summary>The keys each key or account of it serves alone, by its name
    /// in lower case or its key's id.</summary>
    public Dictionary<string, List<string>>? AccountModels_ { get; set; }
    /// <summary>Where a model of it is in a routing group, by model id.</summary>
    public Dictionary<string, List<string>>? Groups { get; set; }
    /// <summary>Its key list with each one on or off, for the editor.</summary>
    public List<string>? Chosen { get; set; }
    /// <summary>The APIs a model can be asked on, the model ids by key.</summary>
    public Dictionary<string, ModelPrefJson>? ModelPrefs { get; set; }
}

public sealed class BalanceTokenJson
{
    public bool Takes { get; set; }
    public bool Set { get; set; }
}

public sealed class VertexJson
{
    public string? Project { get; set; }
    public string? Location { get; set; }
    public string? Impersonate { get; set; }
    public string? Credentials { get; set; }
}

public sealed class ZhipuTeamJson
{
    public string? Org { get; set; }
    public string? Project { get; set; }
}

/// <summary>A Volcengine access key or a Tencent Cloud SecretId: whose, the
/// ID, and whether a Secret is saved; never the Secret itself.</summary>
public sealed class AccessKeyJson
{
    public string Vendor { get; set; } = "";
    public string Id { get; set; } = "";
    public bool SecretSet { get; set; }
}

/// <summary>The names, levels, images and API a model is asked on, as the
/// editor's Names & levels sets them (provider.ModelPref).</summary>
public sealed class ModelPrefJson
{
    public string? Name { get; set; }
    public List<string>? Efforts { get; set; }
    public bool? Reasonless { get; set; }
    public bool? Images { get; set; }
    public string? Api { get; set; }
    public string? Same { get; set; }
    public PriceJson? Price { get; set; }
    public string? Kind { get; set; }
}

/// <summary>catalog.Price: what a model costs, USD per million tokens.</summary>
public sealed class PriceJson
{
    public double Input { get; set; }
    public double Output { get; set; }
    [JsonPropertyName("cache_read")] public double CacheRead { get; set; }
    [JsonPropertyName("cache_write")] public double CacheWrite { get; set; }
    [JsonPropertyName("cache_write_1h")] public double? CacheWrite1h { get; set; }
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
    /// <summary>The model's own name, when the user gave it another.</summary>
    public string? Default { get; set; }
    public string Icon { get; set; } = "";
    /// <summary>Exposed to agents (modelJSON's "on").</summary>
    public bool On { get; set; }
    /// <summary>Its context window before the user's, in tokens.</summary>
    public int Listed { get; set; }
    public int Context { get; set; }
    /// <summary>The reply limit agents are told.</summary>
    public int Output { get; set; }
    /// <summary>The reasoning levels it takes.</summary>
    public List<string>? Efforts { get; set; }
    public List<string>? Kept { get; set; }
    public bool Given { get; set; }
    public bool Reasonless { get; set; }
    /// <summary>Agents are told it can see images.</summary>
    public bool Images { get; set; }
    public bool ImageSet { get; set; }
    public bool Free { get; set; }
    /// <summary>The credits a request costs the subscription, as a multiple.</summary>
    public double Rate { get; set; }
    public double RateWas { get; set; }
    /// <summary>The one API the user said it is asked on.</summary>
    public string? Api { get; set; }
    /// <summary>The APIs its vendor's list says it is served on.</summary>
    public List<string>? Auto { get; set; }
    /// <summary>The provider has a URL for none of the APIs it is on: agents
    /// aren't offered it.</summary>
    public bool NoApi { get; set; }
    public string? Same { get; set; }
    /// <summary>What the user said it makes: image or video.</summary>
    public string? Kind { get; set; }
    public string? Desktop { get; set; }
    public PriceJson? Price { get; set; }
    public PriceJson? List { get; set; }
}

public sealed class ProviderAgent
{
    public string Agent { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string AgentIcon { get; set; } = "";
    /// <summary>This agent is on one of this provider's models now.</summary>
    public bool Current { get; set; }
    public string? Model { get; set; }
    /// <summary>Through this routing group, one of whose members it is.</summary>
    public string? Group { get; set; }
}

public sealed class AccountJson
{
    public string User { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>The agent's id (codex, copilot), for the account's own words.</summary>
    public string Agent { get; set; } = "";
    /// <summary>The agent's name, for "from Codex CLI's sign-in".</summary>
    public string AgentName { get; set; } = "";
    public string AgentIcon { get; set; } = "";
    /// <summary>The agent's accounts magpie remembers, to switch between.</summary>
    public List<LoginJson>? Logins { get; set; }
    /// <summary>A plugin's provider id, for a plugin beside a built-in.</summary>
    public string? Builtin { get; set; }
    /// <summary>The distro Claude Code runs in, on a Windows with none of its
    /// own.</summary>
    public string? Wsl { get; set; }
}

/// <summary>provider.Login: one saved account of an agent.</summary>
public sealed class LoginJson
{
    public string Agent { get; set; } = "";
    public string User { get; set; } = "";
    public string? Plan { get; set; }
    public DateTimeOffset Seen { get; set; }
    /// <summary>The agent is signed in to this one now.</summary>
    public bool Active { get; set; }
    /// <summary>In use: the active one, or next in line.</summary>
    public bool On { get; set; }
    /// <summary>The vendor refused to refresh its sign-in: it has to be
    /// signed in again.</summary>
    public string? Lapsed { get; set; }
    /// <summary>The agent's own sign-in, which magpie only reads.</summary>
    public bool Own { get; set; }
    /// <summary>The account the agent is signed in to, passed over by the
    /// gateway while another is on.</summary>
    public bool Paused { get; set; }
    /// <summary>The account magpie signed the agent out of when it was spent,
    /// and signs it back in to once it has room again.</summary>
    public bool Returns { get; set; }
}

public sealed class MoveJson
{
    public string Package { get; set; } = "";
    /// <summary>"" (built-in, never moved), "plugin", "back" or "failed".</summary>
    public string State { get; set; } = "";
    public string? Error { get; set; }
    public MoveWhyJson? Why { get; set; }
}

/// <summary>provider.MoveWhy: a failed move's reason, said in the reader's
/// language from what it names.</summary>
public sealed class MoveWhyJson
{
    public string Kind { get; set; } = "";
    public string? Version { get; set; }
    public string? Min { get; set; }
    public string? Models { get; set; }
}

public sealed class PresetJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    /// <summary>"vendor", "relay", "local", "remote" or "partner".</summary>
    public string Kind { get; set; } = "";
    public string Website { get; set; } = "";
    public string KeysUrl { get; set; } = "";
    public string Chat { get; set; } = "";
    public string Responses { get; set; } = "";
    public string Anthropic { get; set; } = "";
    public string? Decide { get; set; }
    public bool NoKey { get; set; }
    public string? KeyHint { get; set; }
    public bool Sponsored { get; set; }
    /// <summary>One line under the name.</summary>
    public string? Note { get; set; }
    /// <summary>The add sheet's name for it, when Name is long.</summary>
    public string? Short { get; set; }
    /// <summary>The base-URL choices a relay's regions or a vendor's plans
    /// make.</summary>
    public List<RegionJson>? Regions { get; set; }
    public string? RegionLabel { get; set; }
    public List<string>? HeaderHints { get; set; }
    /// <summary>What the plan's models' ids start with; the rest of the
    /// vendor's list is left out.</summary>
    public string? Only { get; set; }
    public List<string>? Models { get; set; }
    /// <summary>The vendor has no list to ask for; Models are its list.</summary>
    public bool NoList { get; set; }
    /// <summary>An example of the address the user's resource is at (Azure).</summary>
    public string? Endpoint { get; set; }
    public string? EndpointHint { get; set; }
    public string? EndpointNeeded { get; set; }
    public bool Added { get; set; }
    public bool ZhipuTeam { get; set; }
    public bool AccessKey { get; set; }
    public string? AccessKeyVendor { get; set; }
    public List<string>? AccessKeyRegions { get; set; }
    public bool Vertex { get; set; }
    public bool OwnClient { get; set; }
    /// <summary>A partner's tagline by language.</summary>
    public Dictionary<string, string>? Notes { get; set; }
    public List<string>? Langs { get; set; }
    /// <summary>A partner listed since the add sheet last showed.</summary>
    public bool New { get; set; }
}

public sealed class RegionJson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Chat { get; set; } = "";
    public string Responses { get; set; } = "";
    public string Anthropic { get; set; } = "";
    public bool Lists { get; set; }
    public string? KeysUrl { get; set; }
    public string? Website { get; set; }
}

public sealed class ExcludedJson
{
    public string Agent { get; set; } = "";
    public string User { get; set; } = "";
    public string Why { get; set; } = "";
    /// <summary>The agent's name and logo, as the row shows them.</summary>
    public string AgentName { get; set; } = "";
    public string AgentIcon { get; set; } = "";
    /// <summary>The provider the removed accounts stand behind, to add back.</summary>
    public string? Provider { get; set; }
    /// <summary>Dismissed: the Add sheet offers it back.</summary>
    public bool Quiet { get; set; }
    /// <summary>Signed out here: its saved accounts are listed nowhere else.</summary>
    public bool SignedOut { get; set; }
    public List<string>? Users { get; set; }
    /// <summary>The keychain didn't give its sign-in: read it once more.</summary>
    public bool Retry { get; set; }
}

/// <summary>provider.SignInState: a subscription's sign-in as it stands.</summary>
public class SignInStateJson
{
    public string Id { get; set; } = "";
    public string Agent { get; set; } = "";
    /// <summary>The vendor's page, to open or copy.</summary>
    public string Url { get; set; } = "";
    /// <summary>What to type there, for a device code.</summary>
    public string? Code { get; set; }
    /// <summary>"installing", "waiting", "done", "failed" or "canceled".</summary>
    public string State { get; set; } = "";
    /// <summary>A callback URL can also finish this sign-in.</summary>
    public bool PasteCallback { get; set; }
    /// <summary>A plugin's sign-in finished by the code its page shows.</summary>
    public bool PasteCode { get; set; }
    /// <summary>A sign-in an API key made on KeysUrl also finishes.</summary>
    public bool PasteKey { get; set; }
    public string? KeysUrl { get; set; }
    /// <summary>A plugin's words for its page.</summary>
    public string? Instructions { get; set; }
    /// <summary>The CLI being installed before the sign-in can start.</summary>
    public string? Installing { get; set; }
    public string? User { get; set; }
    public string? Plan { get; set; }
    /// <summary>The agent was signed in to it too.</summary>
    public bool Using { get; set; }
    /// <summary>An account listed already: its sign-in was renewed.</summary>
    public bool Again { get; set; }
    public string? Error { get; set; }
    /// <summary>Why the port the browser is sent back to couldn't be opened:
    /// "reserved" or "busy".</summary>
    public string? CallbackBlocked { get; set; }
    public int CallbackPort { get; set; }
    public string? Proxy { get; set; }
}

/// <summary>The answer to POST /api/signin, with why its page didn't open.</summary>
public sealed class SignInOpenedJson : SignInStateJson
{
    public string? OpenError { get; set; }
}

/// <summary>What POST /api/signin/import answered (provider.ImportedAccount).</summary>
public sealed class ImportedAccountJson
{
    /// <summary>The account's email, or "#n" (its place in the file).</summary>
    public string User { get; set; } = "";
    /// <summary>"added", "updated", "exists" or "failed".</summary>
    public string Status { get; set; } = "";
    public string? Plan { get; set; }
    public string? Error { get; set; }
    /// <summary>The place (from 1) of a file that held no account.</summary>
    public int File { get; set; }
}

public sealed class ImportAnswerJson
{
    public List<ImportedAccountJson> Results { get; set; } = [];
    public ProvidersJson Providers { get; set; } = new();
}

/// <summary>A plugin provider's way to sign in (plugin.Method).</summary>
public sealed class PluginMethodJson
{
    /// <summary>"browser", "api" or "oauth".</summary>
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Placeholder { get; set; }
}

/// <summary>A plugin's question, from POST /api/plugin-signin/prompt.</summary>
public sealed class PluginPromptJson
{
    /// <summary>"text", "select" or "password".</summary>
    public string Type { get; set; } = "";
    public string Key { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Placeholder { get; set; }
    public List<PluginPromptOptionJson>? Options { get; set; }
}

public sealed class PluginPromptOptionJson
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Hint { get; set; }
}

/// <summary>The answer to POST /api/plugin-signin/prompt.</summary>
public sealed class PluginPromptAnswerJson
{
    /// <summary>The next question, when there is one.</summary>
    public PluginPromptJson? Prompt { get; set; }
    public string? Error { get; set; }
}

/// <summary>pluginSubJSON's methods, which Api.cs's PluginSubJson lacked:
/// the ways a plugin's provider can be signed in to.</summary>
public sealed class PluginMethodListJson
{
    public List<PluginMethodJson> Methods { get; set; } = [];
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
    /// <summary>The ways this provider can be signed in to.</summary>
    public List<PluginMethodJson> Methods { get; set; } = [];
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
    /// <summary>Those answered after a try that failed first (usage.Totals).</summary>
    public int Retried { get; set; }
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
    /// <summary>The rows the filter keeps, on every page.</summary>
    public int Total { get; set; }
    public string Bucket { get; set; } = "";
    public string? Day { get; set; }
    /// <summary>The agents with calls in the period, for the filter.</summary>
    public List<NamedJson> Agents { get; set; } = [];
    public List<NamedJson> Providers { get; set; } = [];
    public List<string> Purposes { get; set; } = [];
}

/// <summary>
/// One call of the ledger (usageapi.Row, usage.Record with the names the
/// page shows it by). The names are the record's own: it spells its tokens
/// <c>in</c>, <c>out</c>, <c>cache_read</c> and its error <c>err</c>, so
/// those are written out here rather than derived from the property.
/// </summary>
public sealed class LedgerRowJson
{
    [System.Text.Json.Serialization.JsonPropertyName("route_id")]
    public long RouteId { get; set; }
    /// <summary>The call's time (<c>t</c>).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("t")]
    public DateTimeOffset T { get; set; }
    public string Agent { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderName { get; set; } = "";
    /// <summary>Where the call went: the provider's host then
    /// (usage.Record.Host).</summary>
    public string? Host { get; set; }
    /// <summary>The model the vendor served (usage.Record.Model).</summary>
    public string Model { get; set; } = "";
    /// <summary>The model the agent asked for: a magpie alias, a group, or
    /// provider/model (usage.Record.Requested, <c>req</c>).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("req")]
    public string? Requested { get; set; }
    public string? Served { get; set; }
    /// <summary>The subscription account that answered the call.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("providerAccount")]
    public string? ProviderAccount { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("in")]
    public int Input { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("out")]
    public int Output { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("cache_read")]
    public int CacheRead { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("cache_write")]
    public int CacheWrite { get; set; }
    public int Reasoning { get; set; }
    /// <summary>The call's ms, from the request to the end of its reply.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("ms")]
    public long Millis { get; set; }
    /// <summary>The ms to its first content; 0 for a reply not streamed.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("ttft_ms")]
    public long Ttft { get; set; }
    public double Cost { get; set; }
    public bool Priced { get; set; }
    public bool Swapped { get; set; }
    public int Status { get; set; }
    /// <summary>Why the call failed, in the vendor's words (usage.Record.Error,
    /// <c>err</c>).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("err")]
    public string? Error { get; set; }
    /// <summary>What the vendor's body called the error (rate_limit_error).</summary>
    public string? ErrType { get; set; }
    public string? Purpose { get; set; }
    public string? Session { get; set; }
    /// <summary>The other computer the call was made on, when sync brought
    /// it (<c>computer</c>); the name is usageapi.Row's own.</summary>
    public string? ComputerName { get; set; }
    public string? CallerKeyLabel { get; set; }
    /// <summary>What the agent made the call for, when it isn't a turn of
    /// the conversation (a Codex subagent's review, compact…).</summary>
    public string? Kind { get; set; }
    public string? Access { get; set; }
    /// <summary>The computer whose magpie passed the call on, for one a
    /// Remote magpie provider forwarded.</summary>
    public string? Via { get; set; }
    /// <summary>"log" for a call read from an agent's own session file,
    /// which the gateway never saw; "" for one it logged.</summary>
    public string? Source { get; set; }
    /// <summary>How many tries the gateway made at the request this row ends,
    /// when there was more than one.</summary>
    public int Tries { get; set; }
    /// <summary>This try failed and the request was handed on to the next.</summary>
    public bool HandedOn { get; set; }
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
