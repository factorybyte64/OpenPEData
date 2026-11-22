using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace OpenPEData.Viewer;

public partial class MainWindow : Window
{
    private string? _path;
    /// <summary>The loaded project, opened once; verify, cross references and program information read from it.</summary>
    private TiaProject? _project;
    private TreeNode? _root;
    /// <summary>Top level of the tree: the project node, then the OpenPEData views (overview, lists, cross-references,
    /// integrity check, export).</summary>
    private readonly System.Collections.ObjectModel.ObservableCollection<TreeNode> _top = [];
    /// <summary>Data of the analysis views for the loaded project.</summary>
    private AnalysisContext? _analysis;
    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _filterTimer;
    /// <summary>The one parser run --proginfo of the loaded project (all PLCs); null until a node "Program info" is opened.</summary>
    private ParserRun<JsonArray>? _progInfo;
    /// <summary>The parser run --verify --json; null until the node "Integrity check" is opened.</summary>
    private ParserRun<JsonObject>? _verify;
    /// <summary>The parser run --xref (result null: no XRef.db); null until "Cross-references" or a cross reference tab is opened.</summary>
    private ParserRun<JsonObject?>? _xref;

    /// <summary>A parser run in the background, started on demand and kept until the project is loaded again.</summary>
    private sealed class ParserRun<T>(TiaProject project)
    {
        public TiaProject Project { get; } = project;
        public CancellationTokenSource Cts { get; } = new();
        public Task<T>? Task { get; set; }
    }

    public MainWindow()
    {
        InitializeComponent();
        Title = AppVersion.Title();
        Tree.ItemsSource = _top;
        InitUpdates();
        InitSourceExport();
        DetailViews.CrossRefsPending = CrossRefsState;
        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            foreach (var n in _top) n.ApplyFilter(FilterBox.Text.Trim());
        };
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnOpenFile(this, new RoutedEventArgs())), Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => { if (_path != null) _ = LoadAsync(_path); }), Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => FilterBox.Focus()), Key.F, ModifierKeys.Control));
        Loaded += async (_, _) =>
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToList();
            string? Opt(string name)
            {
                var i = args.IndexOf(name);
                if (i < 0 || i + 1 >= args.Count) return null;
                var v = args[i + 1];
                args.RemoveRange(i, 2);
                return v;
            }
            var select = Opt("--select");
            var tab = Opt("--tab");
            var snapshot = Opt("--snapshot");
            var dump = Opt("--dump");
            var screenPng = Opt("--screen-png");
            var compare = Opt("--compare");
            var settingsPng = Opt("--settings-snapshot");  // file.png or file.png:Page (documentation of the settings dialog)
            ScreenRenderer.Language = Opt("--lang");
            ScreenRenderer.RuntimeVisibility = args.Remove("--runtime-visibility") || ViewerSettings.Current.RuntimeVisibility;
            // the switch "Like runtime" sits on the screen preview: draw the current screen again
            ScreenRenderer.RuntimeVisibilityChanged = () => { if (Tree.SelectedItem is TreeNode n) ShowNode(n); };
            var verify = args.Remove("--verify");
            var xref = args.Remove("--xref");
            var proginfo = args.Remove("--proginfo");
            if (args.Count > 0) await LoadAsync(args[0]);
            else if (ViewerSettings.Current is { ReopenLastProject: true, LastProject: { } last } && (File.Exists(last) || Directory.Exists(last)))
                await LoadAsync(last);
            // --xref, --proginfo, --verify: run at once; without --select the node is selected
            async Task RunOption(TreeNode? node, Func<Task> run)
            {
                if (select == null && node != null)
                {
                    for (var p = node.Parent; p != null; p = p.Parent) p.IsExpanded = true;
                    node.IsSelected = true;
                }
                try { await run(); } catch { /* shown in the detail pane */ }
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);  // result -> detail tabs
            }
            if (xref) await RunOption(RootNode(NodeKind.CrossRefs), EnsureXref);
            if (proginfo) await RunOption(FindProgInfoNodes().FirstOrDefault(), EnsureProgInfo);
            if (verify) await RunOption(RootNode(NodeKind.Verify), EnsureVerify);
            if (select != null)
            {
                SelectByPath(select);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            }
            if (tab != null)
                foreach (var t in DetailTabs.Items.OfType<TabItem>())
                    if ((t.Header as string ?? "").StartsWith(tab, StringComparison.OrdinalIgnoreCase)) { t.IsSelected = true; break; }
            if (tab != null) await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (screenPng != null)
            {
                SaveScreen(screenPng, compare);
                if (snapshot == null && dump == null) { Close(); return; }
            }
            if (dump != null)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                File.WriteAllText(dump, DumpState(), new System.Text.UTF8Encoding(false));
                if (snapshot == null) Close();
            }
            if (settingsPng != null)
            {
                var cut = settingsPng.LastIndexOf(':');
                var page = cut > 1 ? settingsPng[(cut + 1)..] : null;
                var file = cut > 1 ? settingsPng[..cut] : settingsPng;
                var dlg = new SettingsWindow(page) { Owner = this, WindowStartupLocation = WindowStartupLocation.Manual, Left = -4000 };
                dlg.Show();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Snapshot.Save(dlg, file);
                dlg.Close();
                if (snapshot == null) { Close(); return; }
            }
            if (snapshot != null)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(400);
                Snapshot.Save(this, snapshot);
                Close();
            }
        };
    }

    /// <summary>Option --screen-png: the selected screen at 1:1 as PNG; with --compare a reference screenshot, the
    /// comparison image (file_cmp.png) and the share of differing pixels (file.txt).</summary>
    private void SaveScreen(string file, string? reference)
    {
        if (Tree.SelectedItem is not TreeNode { Kind: NodeKind.Screen, Data: JsonObject screen })
        {
            File.WriteAllText(file + ".txt", "no screen selected\n");
            return;
        }
        var bmp = ScreenRenderer.ToBitmap(ScreenRenderer.Render(screen, TreeBuilder.Arr(screen["items"])));
        ScreenRenderer.SavePng(bmp, file);
        if (reference == null) return;
        var cmp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file))!, Path.GetFileNameWithoutExtension(file) + "_cmp.png");
        var share = ScreenRenderer.Compare(bmp, reference, cmp);
        File.WriteAllText(file + ".txt", string.Format(CultureInfo.InvariantCulture, "differing pixels: {0:P2}\n", share));
    }

    /// <summary>Text dump of the visible tree, the detail tabs and the selected tab (option --dump, for checks without a screen).</summary>
    private string DumpState()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("STATUS: " + StatusText.Text);
        sb.AppendLine("TREE:");
        void Walk(TreeNode n, int d)
        {
            if (!n.IsVisible) return;
            sb.AppendLine($"{new string(' ', d * 2)}{(n.IsSelected ? "*" : "-")} {n.Display}  ({n.Kind})");
            if (n.IsExpanded) foreach (var c in n.Children) Walk(c, d + 1);
        }
        foreach (var n in _top) Walk(n, 1);
        sb.AppendLine("DETAIL: " + DetailPath.Text + "  |  " + DetailTitle.Text);
        sb.AppendLine("TABS: " + string.Join(" | ", DetailTabs.Items.OfType<TabItem>().Select(t => (t.IsSelected ? "*" : "") + t.Header)));
        switch ((DetailTabs.SelectedItem as TabItem)?.Content)
        {
            case TextBox tb:
                sb.AppendLine(tb.Text.Length > 6000 ? tb.Text[..6000] + "\n…" : tb.Text);
                break;
            case DataGrid g:
                sb.AppendLine(string.Join(" | ", g.Columns.Select(c => c.Header)));
                if (g.ItemsSource is System.Data.DataView v)
                    foreach (var r in v.Cast<System.Data.DataRowView>().Take(60))
                        sb.AppendLine(string.Join(" | ", r.Row.ItemArray.Select(x => x is DBNull ? "" : x?.ToString())));
                break;
            case DockPanel dp when dp.Children.OfType<Viewbox>().FirstOrDefault() is { } vb:
                sb.AppendLine("Buttons: " + string.Join(", ", dp.Children.OfType<Button>().Select(b => b.Content)));
                var canvas = ((vb.Child as Border)?.Child as Canvas)?.Children.OfType<Canvas>().FirstOrDefault();
                sb.AppendLine($"Preview {canvas?.Width}x{canvas?.Height}, objects {canvas?.Children.Count}");
                foreach (var b in canvas?.Children.OfType<Border>() ?? [])
                    sb.AppendLine($"  {Canvas.GetLeft(b)},{Canvas.GetTop(b)} {b.Width}x{b.Height} {(b.Child as TextBlock)?.Text} {b.Background}");
                break;
        }
        return sb.ToString();
    }

    /// <summary>Selects a node by display-name path segments separated by "/" (each segment: prefix match).</summary>
    private void SelectByPath(string path)
    {
        var node = _root;
        var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        // the first segment may name a top-level node (the project or a view); otherwise the path starts in the project
        if (segs.Count > 0 && _top.FirstOrDefault(t => t.Display.StartsWith(segs[0], StringComparison.OrdinalIgnoreCase)) is { } top)
        {
            node = top;
            segs.RemoveAt(0);
        }
        foreach (var seg in segs)
        {
            var next = node?.Children.FirstOrDefault(c => c.Display.StartsWith(seg, StringComparison.OrdinalIgnoreCase));
            if (next == null) break;
            node!.IsExpanded = true;
            node = next;
        }
        if (node != null) node.IsSelected = true;
    }

    // ---------- opening ----------

    /// <summary>"Open TIA file": every file the parser reads, for every supported TIA Portal version (projects .apNN,
    /// project archives .zapNN, global libraries .alNN, library archives .zalNN).</summary>
    public static string TiaFileFilter()
    {
        var nums = TiaVersion.Supported.Select(v => v.Major.ToString(CultureInfo.InvariantCulture)).Distinct().ToList();
        var label = string.Join(", ", TiaVersion.Supported.Select(v => v.ToString()));
        string Pat(params string[] kinds) => string.Join(";", kinds.SelectMany(k => nums.Select(n => $"*.{k}{n}")));
        return $"All TIA files – {label} ({Pat("ap", "zap", "al", "zal")})|{Pat("ap", "zap", "al", "zal")}|" +
               $"Projects ({Pat("ap")})|{Pat("ap")}|Project archives ({Pat("zap")})|{Pat("zap")}|" +
               $"Global libraries ({Pat("al")})|{Pat("al")}|Library archives ({Pat("zal")})|{Pat("zal")}|All files (*.*)|*.*";
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Open TIA file", Filter = TiaFileFilter() };
        if (dlg.ShowDialog(this) == true) _ = LoadAsync(dlg.FileName);
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        if (_path != null) _ = LoadAsync(_path);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) _ = LoadAsync(files[0]);
    }

    private async Task LoadAsync(string path)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        SetBusy(true, $"Reading {path} …");
        var sw = Stopwatch.StartNew();
        try
        {
            var (project, res) = await ParserRunner.OpenAsync(path, cts.Token);
            if (cts.IsCancellationRequested)
            {
                project?.Dispose();
                return;
            }
            if (project == null || res.Json == null)
            {
                SetBusy(false, "Error while reading.");
                ShowError("The parser returned no JSON.", res.StdErr);
                return;
            }
            _path = path;
            _project?.Dispose();
            _project = project;
            DetailViews.CrossRefs = null;
            _progInfo?.Cts.Cancel();
            _verify?.Cts.Cancel();
            _xref?.Cts.Cancel();
            _progInfo = null;
            _verify = null;
            _xref = null;
            _root = TreeBuilder.Build(res.Json, Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/')));
            _analysis = new AnalysisContext(res.Json, path, _root.Name)
            {
                ParserMessages = string.IsNullOrWhiteSpace(res.StdErr) ? null : res.StdErr.Trim(),
                Navigate = kind => { if (RootNode(kind) is { } n) n.IsSelected = true; },
                Status = text => SetBusy(false, text),
            };
            _top.Clear();
            _top.Add(_root);
            // TIA shows these next to the project tree (task card "Libraries", tab "Plant objects")
            if (TreeBuilder.LibraryNode(res.Json) is { } libs) _top.Add(libs);
            if (TreeBuilder.PlantObjectsNode(res.Json) is { } plant) _top.Add(plant);
            foreach (var v in AnalysisViews.ViewNodes(_analysis)) _top.Add(v);
            _top.Add(AnalysisViews.ExportNode());
            if (!Environment.GetCommandLineArgs().Any(a => a is "--dump" or "--snapshot" or "--screen-png"))
            {
                var settings = ViewerSettings.Current.Clone();
                settings.LastProject = Path.GetFullPath(path);
                settings.Save();
            }
            Title = AppVersion.Title(_root.Name);
            ReloadButton.IsEnabled = true;
            if (FilterBox.Text.Length > 0) foreach (var n in _top) n.ApplyFilter(FilterBox.Text.Trim());
            _root.IsSelected = true;
            FillLanguages(res.Json);
            var st = res.Json["statistics"];
            SetBusy(false, $"{path}  ·  {st?["objects"]} objects, {st?["records"]} records, " +
                           $"hash errors {st?["hashErrors"]}  ·  read in {sw.Elapsed.TotalSeconds:0.0} s" +
                           (string.IsNullOrWhiteSpace(res.StdErr) ? "" : "  ·  parser messages: see Overview"));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetBusy(false, "Error while reading.");
            ShowError(ex.Message, ex.ToString());
        }
    }

    /// <summary>A view node at the top level of the tree.</summary>
    private TreeNode? RootNode(NodeKind kind) => _top.FirstOrDefault(c => c.Kind == kind);

    /// <summary>Starts a background parser run once per loaded project and returns its task; a failed run stays until
    /// "Try again".</summary>
    private Task<T> Ensure<T>(ref ParserRun<T>? field, Func<ParserRun<T>, Task<T>> start)
    {
        if (_project == null) return Task.FromException<T>(new InvalidOperationException("No project loaded."));
        if (field is { Task: { } running } && field.Project == _project) return running;
        var run = field = new ParserRun<T>(_project);
        run.Task = start(run);
        return run.Task;
    }

    private Task<JsonObject> EnsureVerify() => Ensure(ref _verify, RunVerify);

    private async Task<JsonObject> RunVerify(ParserRun<JsonObject> run)
    {
        SetBusy(true, "Checking integrity …");
        try
        {
            var res = await ParserRunner.RunAsync(run.Project, run.Cts.Token, "--verify", "--json");
            if (res.Json is not JsonObject v)
                throw new InvalidOperationException("The check returned no JSON." + StdErrTail(res.StdErr));
            var ok = v["ok"]?.GetValue<bool>() == true;
            if (_verify == run)
            {
                if (RootNode(NodeKind.Verify) is { } node)
                {
                    node.Data = v;
                    node.Suffix = ok ? "OK" : "ERROR";
                }
                SetBusy(false, ok ? "Integrity check: all OK." : "Integrity check: deviations found.");
            }
            return v;
        }
        catch (Exception ex)
        {
            if (_verify == run && ex is not OperationCanceledException) SetBusy(false, "Check failed.");
            throw;
        }
    }

    private Task<JsonObject?> EnsureXref() => Ensure(ref _xref, RunXref);

    /// <summary>Reads XRef.db (parser --xref) for the node "Cross-references" and the per-object tabs "Used by"/"Uses".</summary>
    private async Task<JsonObject?> RunXref(ParserRun<JsonObject?> run)
    {
        SetBusy(true, "Reading cross-references from XRef.db …");
        try
        {
            var res = await ParserRunner.RunAsync(run.Project, run.Cts.Token, "--xref");
            if (res.Json is not JsonObject x)
            {
                if (res.ExitCode != 0)
                    throw new InvalidOperationException("The parser returned no cross-references." + StdErrTail(res.StdErr));
                if (_xref == run)
                {
                    if (RootNode(NodeKind.CrossRefs) is { } none) none.Suffix = "no XRef.db";
                    SetBusy(false, "The project contains no XRef.db.");
                }
                return null;
            }
            if (_xref == run)
            {
                DetailViews.CrossRefs = x;
                var refs = x["references"] is JsonArray r ? r.Count : 0;
                if (RootNode(NodeKind.CrossRefs) is { } node)
                {
                    node.Data = x;
                    node.Suffix = refs.ToString(CultureInfo.InvariantCulture);
                }
                SetBusy(false, $"Cross-references: {refs} usages, {(x["addresses"] as JsonArray)?.Count ?? 0} addresses " +
                               $"({x["staleRelations"]} entries for deleted objects hidden).");
            }
            return x;
        }
        catch (Exception ex)
        {
            if (_xref == run && ex is not OperationCanceledException) SetBusy(false, "Cross-references could not be read.");
            throw;
        }
    }

    /// <summary>Tab of the overview with the result of a background parser run (integrity check or cross-references):
    /// loading, the error with "Try again", or the result with its own tabs.</summary>
    private UIElement RunTab(TreeNode overview, NodeKind kind)
    {
        Task task = kind == NodeKind.Verify ? EnsureVerify() : EnsureXref();
        if (!task.IsCompletedSuccessfully)
            return kind == NodeKind.Verify
                ? RunState(overview, task, "Checking integrity …", () => _verify = null)
                : RunState(overview, task, "Reading cross-references from XRef.db …", () => _xref = null);
        JsonNode? data = ((Task<JsonObject?>)task).Result;
        if (data == null) return StatePanel(NoXrefText, false, null);
        var inner = new TabControl();
        foreach (var (header, create) in DetailViews.Tabs(new TreeNode(kind == NodeKind.Verify ? "Integrity check" : "Cross-references", kind, data)))
            inner.Items.Add(new TabItem { Header = header, Tag = create });
        inner.SelectionChanged += (_, e) =>
        {
            if (e.OriginalSource == inner && inner.SelectedItem is TabItem { Content: null, Tag: Func<UIElement> make } t) t.Content = make();
        };
        if (inner.Items.Count > 0) ((TabItem)inner.Items[0]!).IsSelected = true;
        return inner;
    }

    private const string NoXrefText = "The project contains no XRef.db, so there are no cross-references. " +
        "TIA Portal creates it in the project folder under XRef.";

    /// <summary>Content of the node "Cross-references" and of the cross reference tabs before the data is there: starts the
    /// run, then shows loading, the error with "Try again", or that the project has no XRef.db.</summary>
    private UIElement CrossRefsState(TreeNode node)
    {
        var task = EnsureXref();
        return task.IsCompletedSuccessfully ? StatePanel(NoXrefText, false, null)
            : RunState(node, task, "Reading cross-references from XRef.db …", () => _xref = null);
    }

    private IEnumerable<TreeNode> FindProgInfoNodes()
    {
        IEnumerable<TreeNode> Walk(TreeNode n) => (n.Kind == NodeKind.ProgramInfo ? [n] : Enumerable.Empty<TreeNode>())
            .Concat(n.Children.SelectMany(Walk));
        return _root == null ? [] : Walk(_root);
    }

    /// <summary>The parser run --proginfo, once per loaded project (it covers all PLCs).</summary>
    private Task<JsonArray> EnsureProgInfo() => Ensure(ref _progInfo, RunProgInfo);

    private async Task<JsonArray> RunProgInfo(ParserRun<JsonArray> run)
    {
        SetBusy(true, "Loading program info …");
        var sw = Stopwatch.StartNew();
        try
        {
            var res = await ParserRunner.RunAsync(run.Project, run.Cts.Token, "--proginfo");
            if (res.Json?["plcs"] is not JsonArray plcs)
                throw new InvalidOperationException("The parser returned no program info." + StdErrTail(res.StdErr));
            if (_progInfo == run) SetBusy(false, $"Program info: {plcs.Count} PLC, read in {sw.Elapsed.TotalSeconds:0.0} s.");
            return plcs;
        }
        catch (Exception ex)
        {
            if (_progInfo == run && ex is not OperationCanceledException) SetBusy(false, "Program info could not be read.");
            throw;
        }
    }

    /// <summary>Detail pane of a node "Program info": its tabs once the run is done, otherwise a loading or error
    /// panel. When the run ends, the pane is rebuilt only if the node is still selected.</summary>
    private void ShowProgInfo(TreeNode node)
    {
        var task = EnsureProgInfo();
        if (task.IsCompletedSuccessfully)
        {
            var plc = TreeBuilder.Str(node.Data?["plc"]);
            if (task.Result.OfType<JsonObject>().FirstOrDefault(p => TreeBuilder.Str(p["name"]) == plc) is { } pi)
                foreach (var (header, create) in ProgramInfoViews.Tabs(pi))
                    DetailTabs.Items.Add(new TabItem { Header = header, Tag = create, Padding = new Thickness(10, 4, 10, 4) });
            else
                DetailTabs.Items.Add(new TabItem { Header = "Info", Padding = new Thickness(10, 4, 10, 4),
                    Content = StatePanel($"The parser returns no program info for the PLC \"{plc}\".", false, null) });
            return;
        }
        DetailTabs.Items.Add(new TabItem { Header = "Program info", Padding = new Thickness(10, 4, 10, 4),
            Content = RunState(node, task, "Loading program info …", () => _progInfo = null) });
    }

    /// <summary>Panel for a run that has not succeeded yet: loading (the node is shown again when the run ends) or the
    /// error with "Try again" (<paramref name="reset"/> drops the failed run).</summary>
    private FrameworkElement RunState(TreeNode node, Task task, string loading, Action reset)
    {
        if (task.IsFaulted)
            return StatePanel(task.Exception!.InnerException!.Message, false, () =>
            {
                reset();
                Refresh(node);
            });
        if (!task.IsCompleted)
            task.ContinueWith(_ => Refresh(node), CancellationToken.None, TaskContinuationOptions.None,
                TaskScheduler.FromCurrentSynchronizationContext());
        return StatePanel(loading, true, null);
    }

    /// <summary>Shows the node again if it is still selected and still in the tree (after the run, or "Try again").</summary>
    private void Refresh(TreeNode node)
    {
        var top = node;
        while (top.Parent != null) top = top.Parent;
        if (!ReferenceEquals(Tree.SelectedItem, node) || !_top.Contains(top)) return;
        ShowNode(node);
    }

    private static FrameworkElement StatePanel(string text, bool busy, Action? retry)
    {
        var panel = new StackPanel { Margin = new Thickness(18), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14,
            Foreground = retry == null ? (System.Windows.Media.Brush)Application.Current.FindResource("Muted") : System.Windows.Media.Brushes.Firebrick,
        });
        if (busy)
            panel.Children.Add(new ProgressBar
            {
                IsIndeterminate = true, Height = 8, Width = 320, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
            });
        if (retry != null)
        {
            var b = new Button
            {
                Content = "Try again", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            b.Click += (_, _) => retry();
            panel.Children.Add(b);
        }
        return panel;
    }

    private void OnAbout(object sender, RoutedEventArgs e) => ShowSettings("About");

    private void SetBusy(bool busy, string text)
    {
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = text;
        Cursor = busy ? Cursors.AppStarting : null;
    }

    private void ShowError(string message, string details) =>
        MessageBox.Show(this, message + (string.IsNullOrWhiteSpace(details) ? "" : "\n\n" + Tail(details, 3000)),
            "OpenPEData Viewer", MessageBoxButton.OK, MessageBoxImage.Error);

    private static string Tail(string s, int n) => s.Length <= n ? s : "…" + s[^n..];

    private static string StdErrTail(string stderr) => string.IsNullOrWhiteSpace(stderr) ? "" : "\n\n" + Tail(stderr.Trim(), 3000);

    // ---------- tree / details ----------

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    /// <summary>Last chosen tab per node kind; first visit uses <see cref="DefaultTab"/>.</summary>
    private readonly Dictionary<NodeKind, string> _lastTab = [];

    private static string[] DefaultTabs(NodeKind k) => k switch
    {
        NodeKind.TagTable or NodeKind.AllTags => ["Tags"],
        NodeKind.Screen or NodeKind.LibVersion => ["Preview"],
        NodeKind.Block => ["Interface", "Elements"],
        NodeKind.PlcType => ["Elements"],
        NodeKind.Verify => ["Properties"],
        NodeKind.CrossRefs => ["Usages"],
        NodeKind.ProgramInfo => ["Resources"],
        _ => [],
    };

    private NodeKind? _shownKind;

    private bool _fillingLanguages;

    /// <summary>Language box: the runtime languages of all HMI devices (parser runtimeLanguages, in their order);
    /// the start value is --lang, else the first one. Empty without HMI.</summary>
    private void FillLanguages(JsonNode json)
    {
        var langs = TreeBuilder.Arr(json["project"]?["devices"])
            .SelectMany(d => TreeBuilder.Arr(d["hmi"]))
            .SelectMany(h => TreeBuilder.Arr(h["runtimeLanguages"]))
            .Where(l => l["enable"] is not JsonValue en || !en.TryGetValue<bool>(out var b) || b)
            .OrderBy(l => l["order"] is JsonValue o && o.TryGetValue<int>(out var i) ? i : int.MaxValue)
            .Select(l => TreeBuilder.Str(l["language"])).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        if (ScreenRenderer.Language is { } cur && !langs.Contains(cur)) langs.Insert(0, cur);
        _fillingLanguages = true;
        LangBox.ItemsSource = langs;
        LangBox.SelectedItem = ScreenRenderer.Language ?? langs.FirstOrDefault();
        ScreenRenderer.Language = LangBox.SelectedItem as string;
        _fillingLanguages = false;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_fillingLanguages || LangBox.SelectedItem is not string lang || lang == ScreenRenderer.Language) return;
        ScreenRenderer.Language = lang;
        if (Tree.SelectedItem is TreeNode node) ShowNode(node);  // draws the current screen again
    }

    private void OnSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeNode node) ShowNode(node);
    }

    private void ShowNode(TreeNode node)
    {
        DetailPath.Text = node.Parent?.PathText ?? "";
        DetailTitle.Text = node.Name;
        _shownKind = null;
        var wanted = new List<string>();
        if (_lastTab.TryGetValue(node.Kind, out var last)) wanted.Add(last);
        wanted.AddRange(DefaultTabs(node.Kind));
        DetailTabs.Items.Clear();
        TabItem? select = null;
        if (node.Kind == NodeKind.ProgramInfo) ShowProgInfo(node);
        else if (AnalysisViews.IsView(node.Kind))
        {
            foreach (var (header, create) in AnalysisViews.Tabs(node, _analysis))
                DetailTabs.Items.Add(new TabItem { Header = header, Tag = create, Padding = new Thickness(10, 4, 10, 4) });
            // the overview also holds the integrity check and the cross-references (parser runs started when opened)
            if (node.Kind == NodeKind.Overview && _analysis != null)
            {
                DetailTabs.Items.Add(new TabItem { Header = "Integrity check", Padding = new Thickness(10, 4, 10, 4),
                    Tag = (Func<UIElement>)(() => RunTab(node, NodeKind.Verify)) });
                DetailTabs.Items.Add(new TabItem { Header = "Cross-references", Padding = new Thickness(10, 4, 10, 4),
                    Tag = (Func<UIElement>)(() => RunTab(node, NodeKind.CrossRefs)) });
            }
        }
        else if (node.Kind == NodeKind.Verify && node.Data == null)
            DetailTabs.Items.Add(new TabItem { Header = "Integrity check", Padding = new Thickness(10, 4, 10, 4),
                Content = RunState(node, EnsureVerify(), "Checking integrity …", () => _verify = null) });
        else if (node.Kind == NodeKind.CrossRefs && node.Data == null)
            DetailTabs.Items.Add(new TabItem { Header = "Cross-references", Padding = new Thickness(10, 4, 10, 4), Content = CrossRefsState(node) });
        else foreach (var (header, create) in DetailViews.Tabs(node))
        {
            var tab = new TabItem { Header = header, Tag = create, Padding = new Thickness(10, 4, 10, 4) };
            DetailTabs.Items.Add(tab);
        }
        foreach (var w in wanted)
        {
            select ??= DetailTabs.Items.OfType<TabItem>().FirstOrDefault(t => BaseHeader(t.Header as string ?? "") == w);
        }
        // prefer the same kind of tab as before, otherwise the most specific one (first list) for containers
        select ??= DetailTabs.Items.OfType<TabItem>().FirstOrDefault();
        _shownKind = node.Kind;
        if (select != null) select.IsSelected = true;
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != DetailTabs) return;
        if (DetailTabs.SelectedItem is not TabItem tab) return;
        if (tab.Content == null && tab.Tag is Func<UIElement> create)
        {
            try { tab.Content = create(); }
            catch (Exception ex) { tab.Content = new TextBox { Text = ex.ToString(), IsReadOnly = true }; }
        }
        if (_shownKind is { } kind) _lastTab[kind] = BaseHeader(tab.Header as string ?? "");
    }

    private static string BaseHeader(string h)
    {
        var i = h.IndexOf(" (", StringComparison.Ordinal);
        return i < 0 ? h : h[..i];
    }
}

public sealed class RelayCommand(Action run) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => run();
}
