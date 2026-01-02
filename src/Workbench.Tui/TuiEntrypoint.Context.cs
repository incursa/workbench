using System;
using System.Collections.Generic;
using Terminal.Gui;
using Terminal.Gui.Trees;
using Workbench.Core;

namespace Workbench.Tui;

public static partial class TuiEntrypoint
{
    private sealed class TuiContext
    {
        public TuiContext(
            string repoRoot,
            WorkbenchConfig config,
            List<WorkItem> allItems,
            string[] workItemStatusOptions,
            string[] workItemTypeOptions,
            string[] docTypeOptions)
        {
            this.RepoRoot = repoRoot;
            this.Config = config;
            this.AllItems = allItems;
            this.FilteredItems = new List<WorkItem>(allItems);
            this.ListItemLookup = new List<WorkItem?>();
            this.LinkTargets = new List<string>();
            this.WorkItemStatusOptions = workItemStatusOptions;
            this.WorkItemTypeOptions = workItemTypeOptions;
            this.DocTypeOptions = docTypeOptions;
        }

        public string RepoRoot { get; }
        public WorkbenchConfig Config { get; set; }
        public List<WorkItem> AllItems { get; set; }
        public List<WorkItem> FilteredItems { get; }
        public List<WorkItem?> ListItemLookup { get; }
        public List<string> LinkTargets { get; }
        public List<string> DocsAll { get; set; } = new();
        public bool DryRunEnabled { get; set; }
        public bool SettingsLoaded { get; set; }
        public bool CodexAvailable { get; set; }
        public string? CodexVersion { get; set; }
        public string? CodexError { get; set; }

        public StatusBar? StatusBar { get; set; }
        public ColorScheme? DefaultScheme { get; set; }
        public ColorScheme? InputScheme { get; set; }
        public object? AutoRefreshToken { get; set; }
        public string? SelectedDocPath { get; set; }

        public Toplevel? Top { get; set; }
        public Window? Window { get; set; }
        public TabView? TabView { get; set; }
        public TabView.Tab? WorkTab { get; set; }
        public TabView.Tab? DocsTab { get; set; }
        public TabView.Tab? SettingsTab { get; set; }
        public TabView.Tab? RepoTab { get; set; }
        public FrameView? NavFrame { get; set; }
        public FrameView? DetailsFrame { get; set; }
        public View? Footer { get; set; }
        public Label? DryRunLabel { get; set; }
        public Label? CommandPreviewLabel { get; set; }
        public Label? GitInfoLabel { get; set; }
        public Label? LastRefreshLabel { get; set; }
        public Button? RefreshButton { get; set; }

        public TextField? FilterField { get; set; }
        public TextField? StatusField { get; set; }
        public ListView? ListView { get; set; }
        public Label? DetailsHeader { get; set; }
        public TextView? DetailsBody { get; set; }
        public Button? StartWorkButton { get; set; }
        public Button? CompleteWorkButton { get; set; }
        public Button? CodexWorkButton { get; set; }
        public Label? CodexHintLabel { get; set; }
        public ListView? LinksList { get; set; }
        public TextField? LinkTypeField { get; set; }
        public Label? LinkHint { get; set; }

        public TextField? DocsFilterField { get; set; }
        public TreeView<ITreeNode>? DocsTree { get; set; }
        public TextView? DocsPreview { get; set; }
        public Label? DocsPreviewHeader { get; set; }

        public ScrollView? SettingsScroll { get; set; }
        public string ConfigPath { get; set; } = string.Empty;
        public string CredentialsPath { get; set; } = string.Empty;
        public string DefaultConfigJson { get; set; } = string.Empty;
        public string[] ThemeOptions { get; set; } = Array.Empty<string>();
        public string[] GithubProviderOptions { get; set; } = Array.Empty<string>();
        public string[] AiProviderOptions { get; set; } = Array.Empty<string>();

        public TextField? DocsRootField { get; set; }
        public TextField? WorkRootField { get; set; }
        public TextField? ItemsDirField { get; set; }
        public TextField? DoneDirField { get; set; }
        public TextField? TemplatesDirField { get; set; }
        public TextField? WorkboardFileField { get; set; }
        public TextField? ThemeField { get; set; }
        public Button? ThemePickButton { get; set; }
        public CheckBox? UseEmojiCheck { get; set; }
        public TextField? AutoRefreshSecondsField { get; set; }

        public TextField? IdWidthField { get; set; }
        public TextField? BugPrefixField { get; set; }
        public TextField? TaskPrefixField { get; set; }
        public TextField? SpikePrefixField { get; set; }

        public TextField? GitBranchPatternField { get; set; }
        public TextField? GitCommitPatternField { get; set; }
        public TextField? GitBaseBranchField { get; set; }
        public CheckBox? GitRequireCleanCheck { get; set; }

        public TextField? GithubProviderField { get; set; }
        public Button? GithubProviderPickButton { get; set; }
        public CheckBox? GithubDefaultDraftCheck { get; set; }
        public TextField? GithubHostField { get; set; }
        public TextField? GithubOwnerField { get; set; }
        public TextField? GithubRepoField { get; set; }

        public TextField? LinkExcludeField { get; set; }
        public TextField? DocExcludeField { get; set; }

        public TextField? AiProviderField { get; set; }
        public Button? AiProviderPickButton { get; set; }
        public TextField? AiOpenAiKeyField { get; set; }
        public Label? AiOpenAiKeyStatusLabel { get; set; }
        public TextField? AiModelField { get; set; }
        public TextField? GithubTokenField { get; set; }
        public Label? GithubTokenStatusLabel { get; set; }

        public string[] WorkItemStatusOptions { get; }
        public string[] WorkItemTypeOptions { get; }
        public string[] DocTypeOptions { get; }
    }
}
