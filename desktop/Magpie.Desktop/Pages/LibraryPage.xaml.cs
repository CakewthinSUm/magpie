using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Library page, drawn here rather than by the page magpie serves: the
/// shared instructions and the sets they are kept in, the MCP servers, and
/// the skills, each with the agents that have it (GET /api/library,
/// POST /api/library/instructions/save, servers/agents, skills/agents,
/// skills/import). Editing text, adding a server and installing a skill stay
/// the page's own, and each section says so.
/// </summary>
public sealed partial class LibraryPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    private LibraryJson _data = new();
    private readonly Dictionary<string, string> _agentNames = new(StringComparer.Ordinal);
    /// <summary>Set while the page's rows are drawn: a switch takes its
    /// IsOn from the data then, and fires Toggled as it is made. Without
    /// this, every redraw would send the state it was just given back as a
    /// click, one write per agent each time the page is read again.</summary>
    private bool _drawing;

    public LibraryPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            // the whole page takes reading every agent's skill folders; the
            // page shows it in one go, as this window has no glance first
            _data = await App.CurrentApp.Client.GetAsync<LibraryJson>("library") ?? new LibraryJson();
            Draw();
        }, Status, Busy);
    }

    /// <summary>Reads the library again and draws every section from it. A
    /// write answers with the whole page too, so a click draws what magpie
    /// wrote, not what was clicked.</summary>
    private void Draw()
    {
        _drawing = true;
        try
        {
            _agentNames.Clear();
            foreach (var a in _data.Agents) _agentNames[a.Id] = a.Name;

            DrawInstructions();
            DrawServers();
            DrawSkills();

            var parts = new List<string>();
            if (_data.Dir.Length > 0) parts.Add("Kept in " + Tilde(_data.Dir));
            if (_data.Partial) parts.Add("skills found in the agents are still being read");
            Footer.Text = string.Join(" · ", parts);
            Notice.IsOpen = false;
        }
        finally { _drawing = false; }
    }

    // ---- instructions ---------------------------------------------------

    private void DrawInstructions()
    {
        var iv = _data.Instructions ?? new InstructionsJson();
        var on = iv.Sets.FirstOrDefault(s => s.Active) ?? iv.Sets.FirstOrDefault();

        SharedText.Text = iv.Shared;
        SharedSetName.Text = on is null ? "" : "· " + SetName(on);
        SharedEmpty.Visibility = iv.Shared.Trim().Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ReadsLine.Text = on is null
            ? ""
            : "They read " + SetName(on) + (on.Text.Trim().Length > 0 ? "" : " (empty)");

        SetList.ItemsSource = iv.Sets.Select(s => new SetRow
        {
            Id = s.Id,
            Name = SetName(s),
            FirstLine = FirstLineOf(s.Text),
            Text = s.Text,
            Action = s.Active ? "On" : "Switch to this",
            CanUse = !s.Active,
            Tag = s.Active ? "In use" : s.File.Length > 0 ? "Your file" : "",
        }).ToList();

        // an agent switched on stays listed, as the page keeps it: it is how
        // the shared instructions are taken back out of its file
        var rows = iv.Agents.Select(a => new InstructionAgentRow
        {
            Agent = a.Agent,
            Name = a.Name,
            Icon = IconOf(a.Icon),
            On = a.On,
            Line = LineOfInstructions(a),
            Problem = ProblemOf("instructions", a.Agent),
        }).ToList();
        InstructionAgentList.ItemsSource = rows;
        NoInstructionAgents.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string SetName(InstructionSetJson s) => s.Name.Length > 0 ? s.Name : "Default";

    private static string FirstLineOf(string text) =>
        text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) is { } line
            ? line.TrimStart('#', ' ')
            : "Empty";

    private static string LineOfInstructions(AgentInstructionsJson a)
    {
        var parts = new List<string>();
        if (a.Path.Length > 0) parts.Add(Tilde(a.Path));
        if (a.Profile) parts.Add("its profile of its owner");
        if (a.Extra.Length > 0) parts.Add("+ something of its own");
        if (a.Override.Length > 0) parts.Add("reads " + Tilde(a.Override) + " instead");
        if (a.Edited) parts.Add("edited in the file");
        if (a.Own > 0) parts.Add(a.Own == 1 ? "1 line of its own" : $"{a.Own} lines of its own");
        return string.Join(" · ", parts);
    }

    /// <summary>What a change couldn't write into that agent, from the last
    /// change the page kept (the view's problems, for servers and skills).</summary>
    private string ProblemOf(string what, string agent)
    {
        foreach (var p in _data.Problems ?? [])
        {
            if (p.What == what && p.Agent == agent) return p.Error;
        }
        return "";
    }

    private async void UseSet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SetRow row }) return;
        await PageKit.RunAsync(async () =>
        {
            // the write answers with the instructions alone: the rest of the
            // page is left as it was read
            var answer = await App.CurrentApp.Client.PostAsync<InstructionsAnswerJson>(
                "library/instructions/save", new { activate = row.Id });
            if (answer?.Instructions is { } iv)
            {
                _data.Instructions = iv;
                DrawInstructions();
            }
            if (answer?.Result?.Problems is { Count: > 0 } problems) Status.Text = problems[0].Error;
        }, Status, Busy);
    }

    private async void InstructionAgent_Toggled(object sender, RoutedEventArgs e)
    {
        if (_drawing) return;
        if (sender is not ToggleSwitch { Tag: InstructionAgentRow row } toggle) return;
        var on = toggle.IsOn;
        await PageKit.RunAsync(async () =>
        {
            var agents = (_data.Instructions?.Agents ?? []).Where(a => a.Agent == row.Agent ? on : a.On)
                .Select(a => a.Agent).ToList();
            var answer = await App.CurrentApp.Client.PostAsync<InstructionsAnswerJson>(
                "library/instructions/save", new { agents });
            if (answer?.Instructions is { } iv)
            {
                _data.Instructions = iv;
                DrawInstructions();
            }
            if (answer?.Result?.Problems is { Count: > 0 } problems) Status.Text = problems[0].Error;
        }, Status, Busy);
    }

    // ---- MCP servers ----------------------------------------------------

    private void DrawServers()
    {
        var rows = _data.Servers.Select(s => new ServerRow
        {
            Name = s.Name,
            Transport = s.Transport,
            Glyph = s.Transport == "stdio" ? "\uE756" : "\uE774",
            Line = s.Transport == "stdio"
                ? string.Join(" ", new[] { s.Command ?? "" }.Concat(s.Args ?? []))
                : s.Url ?? "",
            OnLine = OnLineOf(s.Agents),
            Problem = ProblemOfServer(s),
        }).ToList();
        ServerList.ItemsSource = rows;
        NoServers.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A server's problem: what the last change couldn't write, and
    /// what each agent given it can't take of it (a remote server for Claude
    /// Desktop, SSE for Codex, a variable one can't read).</summary>
    private string ProblemOfServer(LibraryServerJson s)
    {
        var parts = new List<string>();
        foreach (var (agent, why) in s.Problems ?? [])
        {
            parts.Add((_agentNames.GetValueOrDefault(agent, agent)) + ": " + WhyOf(why));
        }
        if (s.SignIn is { Dead: true }) parts.Add("its sign-in ran out");
        return string.Join(" · ", parts);
    }

    private static string WhyOf(string why) => why switch
    {
        "no-remote" => "takes only a command from its own settings",
        "no-sse" => "can't reach a server over SSE",
        _ => why,
    };

    private void ServerAgents_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ServerRow row } button) return;
        var menu = new MenuFlyout();
        var server = _data.Servers.FirstOrDefault(s => s.Name == row.Name);
        if (server is null) return;
        var has = new HashSet<string>(server.Agents);
        foreach (var a in _data.Agents)
        {
            // an agent gone from here is listed only for what magpie gave it
            // to be taken out
            if (a.Gone && !has.Contains(a.Id)) continue;
            var item = new MenuFlyoutItem
            {
                Text = a.Name,
                Tag = a.Id,
                Icon = new FontIcon { Glyph = has.Contains(a.Id) ? "\uE73E" : "\uE738" },
            };
            if (a.Gone) item.Text = a.Name + " (not installed here)";
            else if (server.Problems?.ContainsKey(a.Id) == true) item.Text = a.Name + " — " + WhyOf(server.Problems[a.Id]);
            item.Click += (_, _) => _ = ToggleServerAgentAsync(server, a.Id, !has.Contains(a.Id));
            menu.Items.Add(item);
        }
        menu.ShowAt(button);
    }

    private async Task ToggleServerAgentAsync(LibraryServerJson server, string agent, bool on)
    {
        await PageKit.RunAsync(async () =>
        {
            var agents = server.Agents.Where(a => a != agent).ToList();
            if (on) agents.Add(agent);
            _data = await App.CurrentApp.Client.PostAsync<LibraryJson>(
                "library/servers/agents", new { name = server.Name, agents }) ?? _data;
            Draw();
        }, Status, Busy);
    }

    // ---- skills ---------------------------------------------------------

    private void DrawSkills()
    {
        SkillList.ItemsSource = _data.Skills.Select(s => new SkillRow
        {
            Name = s.Name,
            Description = s.Missing ? "Its folder is gone from the library" : s.Description,
            Line = s.Kind.Length > 0 ? s.Kind + (s.Source is { Length: > 0 } src ? " · " + src : "") : "kept in the library",
            OnLine = OnLineOf(s.Agents),
            Problem = ProblemOfSkill(s),
        }).ToList();
        NoSkills.Visibility = ((System.Collections.IList)SkillList.ItemsSource).Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

        FoundSkillList.ItemsSource = _data.FoundSkills.Select(f => new FoundSkillRow
        {
            Name = f.Name,
            Description = f.Description,
            Line = FoundLineOf(f),
        }).ToList();
        NoFoundSkills.Visibility = ((System.Collections.IList)FoundSkillList.ItemsSource).Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private string ProblemOfSkill(LibrarySkillJson s)
    {
        var parts = new List<string>();
        if (s.Missing) parts.Add("missing from the library");
        foreach (var (agent, why) in s.Problems ?? [])
        {
            parts.Add((_agentNames.GetValueOrDefault(agent, agent)) + ": " + why);
        }
        return string.Join(" · ", parts);
    }

    private string FoundLineOf(FoundSkillJson f)
    {
        var names = f.Agents.Select(a => _agentNames.GetValueOrDefault(a, a)).ToList();
        return names.Count == 0 ? "" : "in " + string.Join(", ", names);
    }

    private void SkillAgents_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SkillRow row } button) return;
        var menu = new MenuFlyout();
        var skill = _data.Skills.FirstOrDefault(s => s.Name == row.Name);
        if (skill is null) return;
        var has = new HashSet<string>(skill.Agents);
        foreach (var a in _data.Agents)
        {
            if (a.Gone && !has.Contains(a.Id)) continue;
            var item = new MenuFlyoutItem
            {
                Text = a.Gone ? a.Name + " (not installed here)" : a.Name,
                Tag = a.Id,
                Icon = new FontIcon { Glyph = has.Contains(a.Id) ? "\uE73E" : "\uE738" },
            };
            item.Click += (_, _) => _ = ToggleSkillAgentAsync(skill, a.Id, !has.Contains(a.Id));
            menu.Items.Add(item);
        }
        menu.ShowAt(button);
    }

    private async Task ToggleSkillAgentAsync(LibrarySkillJson skill, string agent, bool on)
    {
        await PageKit.RunAsync(async () =>
        {
            var agents = skill.Agents.Where(a => a != agent).ToList();
            if (on) agents.Add(agent);
            _data = await App.CurrentApp.Client.PostAsync<LibraryJson>(
                "library/skills/agents", new { name = skill.Name, agents }) ?? _data;
            Draw();
        }, Status, Busy);
    }

    private async void BringIn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FoundSkillRow row }) return;
        await PageKit.RunAsync(async () =>
        {
            _data = await App.CurrentApp.Client.PostAsync<LibraryJson>(
                "library/skills/import", new { name = row.Name }) ?? _data;
            Draw();
        }, Status, Busy);
    }

    // ---- shared ---------------------------------------------------------

    /// <summary>The agents that have it, by name: "On for Claude Code and
    /// Codex", or a line saying it is on none.</summary>
    private string OnLineOf(List<string> agents)
    {
        if (agents.Count == 0) return "Not on any agent yet";
        return "On for " + string.Join(", ", agents.Select(a => _agentNames.GetValueOrDefault(a, a)));
    }

    private static string Tilde(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            return "~" + path[home.Length..];
        return path;
    }

    /// <summary>The agent's own logo, from the page's icons (/api/icons
    /// serves them); none when magpie has none.</summary>
    private static ImageSource? IconOf(string icon)
    {
        if (string.IsNullOrEmpty(icon)) return null;
        var app = App.CurrentApp;
        if (app.Client.BaseUrl.Length == 0) return null;
        return new BitmapImage(new Uri(app.Client.BaseUrl + "icons/" + Uri.EscapeDataString(icon) + ".svg"));
    }

    /// <summary>The section buttons, as the page's three tabs: one panel is
    /// on at a time.</summary>
    private void Sections_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag as string ?? "instructions";
        InstructionsPanel.Visibility = tag == "instructions" ? Visibility.Visible : Visibility.Collapsed;
        ServersPanel.Visibility = tag == "servers" ? Visibility.Visible : Visibility.Collapsed;
        SkillsPanel.Visibility = tag == "skills" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();
}

/// <summary>One set of shared instructions, as its row draws it.</summary>
public sealed class SetRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FirstLine { get; set; } = "";
    public string Text { get; set; } = "";
    public string Action { get; set; } = "";
    public bool CanUse { get; set; }
    /// <summary>"In use" or "Your file", shown beside the name.</summary>
    public string Tag { get; set; } = "";
}

/// <summary>An agent and whether it reads the shared instructions.</summary>
public sealed class InstructionAgentRow
{
    public string Agent { get; set; } = "";
    public string Name { get; set; } = "";
    public string Line { get; set; } = "";
    public string Problem { get; set; } = "";
    public Visibility ProblemVisibility => Problem.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public ImageSource? Icon { get; set; }
    public bool On { get; set; }
}

/// <summary>One MCP server, as its row draws it.</summary>
public sealed class ServerRow
{
    public string Name { get; set; } = "";
    public string Transport { get; set; } = "";
    public string Glyph { get; set; } = "";
    public string Line { get; set; } = "";
    public string OnLine { get; set; } = "";
    public string Problem { get; set; } = "";
    public Visibility ProblemVisibility => Problem.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>One library skill, as its row draws it.</summary>
public sealed class SkillRow
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Line { get; set; } = "";
    public string OnLine { get; set; } = "";
    public string Problem { get; set; } = "";
    public Visibility ProblemVisibility => Problem.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>A skill found in an agent, for the row that brings it in.</summary>
public sealed class FoundSkillRow
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Line { get; set; } = "";
}
