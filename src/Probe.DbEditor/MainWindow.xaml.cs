using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Probe.DbEditor.Models;
using Probe.DbEditor.Services;
using Probe.DbEditor.Themes;
using Probe.DbEditor.Views.Favorites;
using Probe.DbEditor.Views;

namespace Probe.DbEditor;

public partial class MainWindow : Window
{
    private readonly ConnectionProfileStore _profileStore = new();
    private readonly ObservableCollection<ConnectionProfileFolder> _folders = [];
    private readonly ObservableCollection<ConnectionProfile> _profiles = [];
    private readonly ObservableCollection<FavoriteTreeItemViewModel> _favoriteTree = [];
    private readonly List<ProtocolOption> _protocolOptions =
    [
        new("TCP/IP", ConnectionProtocolKind.Tcp),
        new("Windows named pipe", ConnectionProtocolKind.NamedPipe),
        new("SSH tunnel", ConnectionProtocolKind.SshTunnel)
    ];
    private readonly List<TlsOption> _tlsOptions =
    [
        new("Verify full certificate", DatabaseTlsMode.VerifyFull),
        new("Verify CA only", DatabaseTlsMode.VerifyCA),
        new("Require TLS", DatabaseTlsMode.Required),
        new("Prefer TLS", DatabaseTlsMode.Preferred),
        new("Disable TLS", DatabaseTlsMode.Disabled)
    ];
    private readonly List<FavoriteColorOption> _favoriteColorOptions =
    [
        FavoriteColorOption.Create(ConnectionFavoriteColor.None),
        FavoriteColorOption.Create(ConnectionFavoriteColor.Gray),
        FavoriteColorOption.Create(ConnectionFavoriteColor.Green),
        FavoriteColorOption.Create(ConnectionFavoriteColor.Purple),
        FavoriteColorOption.Create(ConnectionFavoriteColor.Blue),
        FavoriteColorOption.Create(ConnectionFavoriteColor.Yellow),
        FavoriteColorOption.Create(ConnectionFavoriteColor.Red),
        FavoriteColorOption.Create(ConnectionFavoriteColor.Orange)
    ];

    private ConnectionProfile? _selectedProfile;
    private FavoriteTreeItemViewModel? _contextFavoriteItem;
    private FavoriteTreeItemViewModel? _favoriteDragCandidate;
    private FavoriteTreeItemViewModel? _renamingFavoriteItem;
    private Point _favoriteDragStart;
    private string _renameOriginalName = "";
    private bool _connectionOperationInProgress;
    private bool _loadingProfile;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowsTitleBarTheme.Apply(this);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ProtocolCombo.ItemsSource = _protocolOptions;
        ProtocolCombo.DisplayMemberPath = nameof(ProtocolOption.Label);
        ProtocolCombo.SelectedIndex = 0;
        TlsModeCombo.ItemsSource = _tlsOptions;
        TlsModeCombo.DisplayMemberPath = nameof(TlsOption.Label);
        TlsModeCombo.SelectedIndex = 0;
        ProfileColorList.ItemsSource = _favoriteColorOptions;
        ProfileColorList.SelectedItem = _favoriteColorOptions[0];

        SavedProfilesTree.ItemsSource = _favoriteTree;
        var favorites = await _profileStore.LoadFavoritesAsync();
        foreach (var folder in favorites.Folders)
        {
            _folders.Add(folder);
        }

        foreach (var profile in favorites.Profiles)
        {
            _profiles.Add(profile);
        }

        NormalizeProfileFolders();
        RefreshFavoriteTree();
        LoadProfile(new ConnectionProfile());
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        LoadProfile(new ConnectionProfile { FolderId = CurrentFavoriteFolderId() });
    }

    private async void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        await AddFolderAsync();
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfilesTree.SelectedItem is not FavoriteTreeItemViewModel item)
        {
            return;
        }

        if (item.Profile is not null)
        {
            await DeleteProfileAsync(item.Profile);
        }
        else if (item.Folder is not null)
        {
            await DeleteFolderAsync(item.Folder);
        }
    }

    private void SavedProfilesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FavoriteTreeItemViewModel { Profile: not null } item)
        {
            LoadProfile(item.Profile);
        }
    }

    private void ProfileColorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingProfile || _selectedProfile is null)
        {
            return;
        }

        _selectedProfile.Color = SelectedFavoriteColor();
    }

    private void SavedProfilesTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var treeItem = FindTreeViewItem(e.OriginalSource);
        _contextFavoriteItem = treeItem?.DataContext as FavoriteTreeItemViewModel;
        if (treeItem is not null)
        {
            treeItem.IsSelected = true;
            e.Handled = true;
        }
    }

    private void SavedProfilesTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _contextFavoriteItem = (
            FindTreeViewItem(e.OriginalSource) ??
            FindTreeViewItem(Mouse.DirectlyOver))?.DataContext as FavoriteTreeItemViewModel;

        FavoriteContextColorMenu.IsEnabled = _contextFavoriteItem is not null;
        FavoriteContextRenameMenuItem.IsEnabled = _contextFavoriteItem is not null;
        FavoriteContextDeleteProfileMenuItem.IsEnabled = _contextFavoriteItem?.Profile is not null;
        FavoriteContextDeleteFolderMenuItem.IsEnabled = _contextFavoriteItem?.Folder is not null;

        foreach (var colorMenuItem in FavoriteContextColorMenu.Items.OfType<MenuItem>())
        {
            colorMenuItem.IsCheckable = true;
            colorMenuItem.IsChecked = ParseFavoriteColor(colorMenuItem.Tag) == _contextFavoriteItem?.Color;
        }
    }

    private void FavoriteContextNewConnection_Click(object sender, RoutedEventArgs e)
    {
        LoadProfile(new ConnectionProfile { FolderId = ContextFavoriteFolderId() });
    }

    private async void FavoriteContextNewFolder_Click(object sender, RoutedEventArgs e)
    {
        await AddFolderAsync();
    }

    private async void FavoriteContextDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_contextFavoriteItem?.Profile is not null)
        {
            await DeleteProfileAsync(_contextFavoriteItem.Profile);
        }
    }

    private async void FavoriteContextDeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_contextFavoriteItem?.Folder is not null)
        {
            await DeleteFolderAsync(_contextFavoriteItem.Folder);
        }
    }

    private void FavoriteContextRename_Click(object sender, RoutedEventArgs e)
    {
        if (_contextFavoriteItem is not null)
        {
            BeginFavoriteRename(_contextFavoriteItem);
        }
    }

    private async void FavoriteContextColor_Click(object sender, RoutedEventArgs e)
    {
        if (_contextFavoriteItem is null || sender is not MenuItem menuItem)
        {
            return;
        }

        var color = ParseFavoriteColor(menuItem.Tag);
        _contextFavoriteItem.Color = color;
        if (_contextFavoriteItem.Profile is not null &&
            _selectedProfile?.Id == _contextFavoriteItem.Profile.Id)
        {
            _selectedProfile.Color = color;
            ProfileColorList.SelectedItem = FavoriteColorOptionFor(color);
        }

        var selectedId = _contextFavoriteItem.Id;
        var selectedKind = _contextFavoriteItem.Kind;
        await SaveFavoritesAsync();
        RefreshFavoriteTree(selectedId, selectedKind);
    }

    private void SavedProfilesTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _favoriteDragStart = e.GetPosition(SavedProfilesTree);
        _favoriteDragCandidate = FindTreeViewItem(e.OriginalSource)?.DataContext as FavoriteTreeItemViewModel;
    }

    private void SavedProfilesTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _favoriteDragCandidate?.Profile is null)
        {
            return;
        }

        var currentPosition = e.GetPosition(SavedProfilesTree);
        if (Math.Abs(currentPosition.X - _favoriteDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _favoriteDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(typeof(ConnectionProfile), _favoriteDragCandidate.Profile);
        DragDrop.DoDragDrop(SavedProfilesTree, data, DragDropEffects.Move);
        _favoriteDragCandidate = null;
    }

    private void SavedProfilesTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = ResolveDroppedProfile(e, out var profile, out var targetFolderId) &&
            !string.Equals(profile.FolderId, targetFolderId, StringComparison.Ordinal)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void SavedProfilesTree_Drop(object sender, DragEventArgs e)
    {
        if (!ResolveDroppedProfile(e, out var profile, out var targetFolderId) ||
            string.Equals(profile.FolderId, targetFolderId, StringComparison.Ordinal))
        {
            return;
        }

        profile.FolderId = targetFolderId;
        if (_selectedProfile?.Id == profile.Id)
        {
            _selectedProfile.FolderId = targetFolderId;
        }

        await SaveFavoritesAsync();
        RefreshFavoriteTree(profile.Id, FavoriteTreeItemKind.Profile);
        e.Handled = true;
    }

    private async void FavoriteRenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FavoriteTreeItemViewModel item })
        {
            await FinishFavoriteRenameAsync(item, commit: true);
        }
    }

    private async void FavoriteRenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FavoriteTreeItemViewModel item })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            await FinishFavoriteRenameAsync(item, commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            await FinishFavoriteRenameAsync(item, commit: false);
            e.Handled = true;
        }
    }

    private void FavoriteRenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is TextBox textBox)
        {
            textBox.Dispatcher.BeginInvoke(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            });
        }
    }

    private void ProtocolCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var protocol = SelectedProtocol();
        PipePanel.Visibility = protocol == ConnectionProtocolKind.NamedPipe ? Visibility.Visible : Visibility.Collapsed;
        SshPanel.Visibility = protocol == ConnectionProtocolKind.SshTunnel ? Visibility.Visible : Visibility.Collapsed;
        TcpHostPanel.Visibility = protocol == ConnectionProtocolKind.NamedPipe ? Visibility.Collapsed : Visibility.Visible;
        TcpPortPanel.Visibility = protocol == ConnectionProtocolKind.NamedPipe ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var profile = ReadProfileFromForm(includeSecrets: true);
            if (!profile.SavePassword)
            {
                profile.Password = "";
            }

            if (!profile.SaveSshPassword)
            {
                profile.SshPassword = "";
            }

            var existing = _profiles.FirstOrDefault(candidate => candidate.Id == profile.Id);
            if (existing is null)
            {
                _profiles.Add(profile);
                RefreshFavoriteTree(profile.Id, FavoriteTreeItemKind.Profile);
            }
            else
            {
                CopyProfile(profile, existing);
                RefreshFavoriteTree(existing.Id, FavoriteTreeItemKind.Profile);
            }

            await SaveFavoritesAsync();
            var savedSecrets = new List<string>();
            if (profile.SavePassword)
            {
                savedSecrets.Add("database password");
            }

            if (profile.SaveSshPassword)
            {
                savedSecrets.Add("SSH password");
            }

            ConnectionStatusText.Text = savedSecrets.Count > 0
                ? $"Profile saved. Protected with Windows DPAPI: {string.Join(", ", savedSecrets)}."
                : "Profile saved. Passwords and SSH passphrases are not persisted.";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = ex.Message;
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        await ConnectCurrentProfileAsync();
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        await TestCurrentProfileAsync();
    }

    private void BrowseSshKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select SSH private key",
            AddExtension = false,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            ShowHiddenItems = true
        };

        var currentPath = SshKeyPathTextBox.Text.Trim();
        var initialDirectory = FindInitialPrivateKeyDirectory(currentPath);
        if (!string.IsNullOrEmpty(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            dialog.FileName = Path.GetFileName(currentPath);
        }

        if (dialog.ShowDialog(this) == true)
        {
            SshKeyPathTextBox.Text = dialog.FileName;
        }
    }

    private async void SavedProfilesTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SavedProfilesTree.SelectedItem is FavoriteTreeItemViewModel { Profile: not null })
        {
            await ConnectCurrentProfileAsync();
        }
    }

    private async Task ConnectCurrentProfileAsync()
    {
        var profile = ReadProfileFromForm(includeSecrets: true);
        await RunConnectionOperationAsync(profile, "Connecting...", async cancellationToken =>
        {
            var session = await OpenSessionAsync(profile, cancellationToken);

            try
            {
                var view = new SessionView(session);
                var tab = new TabItem { Content = view };
                tab.Header = CreateConnectionTabHeader(profile.Name, tab);

                ConnectionsTab.Items.Add(tab);
                ConnectionsTab.SelectedItem = tab;
                ConnectionStatusText.Text = "Connected.";
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
        });
    }

    private async Task TestCurrentProfileAsync()
    {
        var profile = ReadProfileFromForm(includeSecrets: true);
        await RunConnectionOperationAsync(profile, "Testing connection...", async cancellationToken =>
        {
            await using var session = await OpenSessionAsync(profile, cancellationToken);
            ConnectionStatusText.Text = "Connection test succeeded.";
        });
    }

    private async Task<DatabaseSession> OpenSessionAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken)
    {
        var session = new DatabaseSession(profile);
        await session.OpenAsync(cancellationToken);
        return session;
    }

    private async Task RunConnectionOperationAsync(
        ConnectionProfile profile,
        string progressMessage,
        Func<CancellationToken, Task> operation)
    {
        if (_connectionOperationInProgress)
        {
            return;
        }

        _connectionOperationInProgress = true;
        SetConnectionButtonsEnabled(false);
        ConnectionStatusText.Text = progressMessage;

        using var cancellation = new CancellationTokenSource(ConnectionAttemptDefaults.Timeout);
        try
        {
            await operation(cancellation.Token);
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = ConnectionFailureMessage.Create(ex, profile.Protocol);
        }
        finally
        {
            SetConnectionButtonsEnabled(true);
            _connectionOperationInProgress = false;
        }
    }

    private void SetConnectionButtonsEnabled(bool isEnabled)
    {
        TestConnectionButton.IsEnabled = isEnabled;
        ConnectButton.IsEnabled = isEnabled;
    }

    private DockPanel CreateConnectionTabHeader(string title, TabItem tab)
    {
        var panel = new DockPanel
        {
            LastChildFill = false,
            Background = Brushes.Transparent,
            Tag = tab
        };
        panel.PreviewMouseDown += ConnectionTabHeader_PreviewMouseDown;

        var closeButton = new Button
        {
            Content = "x",
            // Content = "×",
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(8, -2, 0, -2),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = FindResource("MutedTextBrush") as Brush ?? Brushes.Gray,
            FontWeight = FontWeights.Bold,
            ToolTip = "Close connection"
        };
        closeButton.Click += (_, e) =>
        {
            e.Handled = true;
            CloseConnectionTab(tab);
        };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(closeButton);
        return panel;
    }

    private void ConnectionTabHeader_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle ||
            sender is not FrameworkElement { Tag: TabItem tab })
        {
            return;
        }

        e.Handled = true;
        CloseConnectionTab(tab);
    }

    private async void CloseConnectionTab(TabItem tab)
    {
        if (tab.Content is SessionView view)
        {
            await view.CloseAsync();
        }

        ConnectionsTab.Items.Remove(tab);
    }

    private void RefreshFavoriteTree(
        string? selectedId = null,
        FavoriteTreeItemKind? selectedKind = null)
    {
        _favoriteTree.Clear();

        var foldersById = _folders.ToDictionary(folder => folder.Id, StringComparer.Ordinal);
        foreach (var folder in _folders.OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var folderItem = FavoriteTreeItemViewModel.ForFolder(folder);
            foreach (var profile in _profiles
                .Where(profile => string.Equals(profile.FolderId, folder.Id, StringComparison.Ordinal))
                .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                folderItem.Children.Add(FavoriteTreeItemViewModel.ForProfile(profile, folder.Color));
            }

            _favoriteTree.Add(folderItem);
        }

        foreach (var profile in _profiles
            .Where(profile => string.IsNullOrWhiteSpace(profile.FolderId) || !foldersById.ContainsKey(profile.FolderId))
            .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            profile.FolderId = "";
            _favoriteTree.Add(FavoriteTreeItemViewModel.ForProfile(profile, ConnectionFavoriteColor.None));
        }

        if (!string.IsNullOrWhiteSpace(selectedId) && selectedKind is not null)
        {
            Dispatcher.BeginInvoke(() => SelectFavoriteTreeItem(selectedId, selectedKind.Value));
        }
    }

    private void NormalizeProfileFolders()
    {
        var folderIds = _folders.Select(folder => folder.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var profile in _profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.FolderId) &&
                                                           !folderIds.Contains(profile.FolderId)))
        {
            profile.FolderId = "";
        }
    }

    private async Task AddFolderAsync()
    {
        var folder = new ConnectionProfileFolder { Name = UniqueFolderName() };
        _folders.Add(folder);
        await SaveFavoritesAsync();
        RefreshFavoriteTree(folder.Id, FavoriteTreeItemKind.Folder);
        _ = Dispatcher.BeginInvoke(() =>
        {
            var item = FindFavoriteTreeItem(folder.Id, FavoriteTreeItemKind.Folder);
            if (item is not null)
            {
                BeginFavoriteRename(item);
            }
        });
    }

    private async Task DeleteProfileAsync(ConnectionProfile profile)
    {
        _profiles.Remove(profile);
        await SaveFavoritesAsync();
        RefreshFavoriteTree();
        if (_selectedProfile?.Id == profile.Id)
        {
            LoadProfile(new ConnectionProfile());
        }
    }

    private async Task DeleteFolderAsync(ConnectionProfileFolder folder)
    {
        _folders.Remove(folder);
        var deletedProfileIds = _profiles
            .Where(profile => string.Equals(profile.FolderId, folder.Id, StringComparison.Ordinal))
            .Select(profile => profile.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var profile in _profiles.Where(profile => deletedProfileIds.Contains(profile.Id)).ToList())
        {
            _profiles.Remove(profile);
        }

        await SaveFavoritesAsync();
        RefreshFavoriteTree();
        if (_selectedProfile is not null && deletedProfileIds.Contains(_selectedProfile.Id))
        {
            LoadProfile(new ConnectionProfile());
        }
    }

    private void BeginFavoriteRename(FavoriteTreeItemViewModel item)
    {
        if (_renamingFavoriteItem is not null)
        {
            _renamingFavoriteItem.IsEditingName = false;
        }

        _renamingFavoriteItem = item;
        _renameOriginalName = item.Name;
        item.IsEditingName = true;
    }

    private async Task FinishFavoriteRenameAsync(FavoriteTreeItemViewModel item, bool commit)
    {
        if (!ReferenceEquals(_renamingFavoriteItem, item))
        {
            return;
        }

        if (!commit)
        {
            item.Name = _renameOriginalName;
        }
        else
        {
            item.Name = string.IsNullOrWhiteSpace(item.Name)
                ? item.IsFolder ? "New folder" : "New connection"
                : item.Name.Trim();
            await SaveFavoritesAsync();
            if (item.Profile is not null && _selectedProfile?.Id == item.Profile.Id)
            {
                _selectedProfile.Name = item.Name;
                ProfileNameTextBox.Text = item.Name;
            }
        }

        item.IsEditingName = false;
        _renamingFavoriteItem = null;
        _renameOriginalName = "";
        if (commit)
        {
            RefreshFavoriteTree(item.Id, item.Kind);
        }
    }

    private bool ResolveDroppedProfile(
        DragEventArgs e,
        out ConnectionProfile profile,
        out string targetFolderId)
    {
        profile = null!;
        targetFolderId = "";
        if (!e.Data.GetDataPresent(typeof(ConnectionProfile)) ||
            e.Data.GetData(typeof(ConnectionProfile)) is not ConnectionProfile droppedProfile)
        {
            return false;
        }

        profile = droppedProfile;
        var target = FindTreeViewItem(e.OriginalSource)?.DataContext as FavoriteTreeItemViewModel;
        targetFolderId = target switch
        {
            { Folder: not null } => target.Folder.Id,
            { Profile: not null } => target.Profile.FolderId,
            _ => ""
        };
        return true;
    }

    private string CurrentFavoriteFolderId()
    {
        return SavedProfilesTree.SelectedItem is FavoriteTreeItemViewModel item
            ? FolderIdForFavoriteContext(item)
            : "";
    }

    private string ContextFavoriteFolderId()
    {
        return _contextFavoriteItem is null ? "" : FolderIdForFavoriteContext(_contextFavoriteItem);
    }

    private static string FolderIdForFavoriteContext(FavoriteTreeItemViewModel item)
    {
        return item switch
        {
            { Folder: not null } => item.Folder.Id,
            { Profile: not null } => item.Profile.FolderId,
            _ => ""
        };
    }

    private static TreeViewItem? FindTreeViewItem(object? originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is TreeViewItem item)
            {
                return item;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private FavoriteTreeItemViewModel? FindFavoriteTreeItem(string id, FavoriteTreeItemKind kind)
    {
        foreach (var item in _favoriteTree)
        {
            var match = FindFavoriteTreeItem(item, id, kind);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static FavoriteTreeItemViewModel? FindFavoriteTreeItem(
        FavoriteTreeItemViewModel item,
        string id,
        FavoriteTreeItemKind kind)
    {
        if (item.Kind == kind && string.Equals(item.Id, id, StringComparison.Ordinal))
        {
            return item;
        }

        foreach (var child in item.Children)
        {
            var match = FindFavoriteTreeItem(child, id, kind);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private void SelectFavoriteTreeItem(string id, FavoriteTreeItemKind kind)
    {
        SavedProfilesTree.UpdateLayout();
        SelectFavoriteTreeItem(SavedProfilesTree, id, kind);
    }

    private static bool SelectFavoriteTreeItem(ItemsControl parent, string id, FavoriteTreeItemKind kind)
    {
        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeItem)
            {
                continue;
            }

            if (item is FavoriteTreeItemViewModel favoriteItem &&
                favoriteItem.Kind == kind &&
                string.Equals(favoriteItem.Id, id, StringComparison.Ordinal))
            {
                treeItem.IsSelected = true;
                treeItem.BringIntoView();
                return true;
            }

            treeItem.IsExpanded = true;
            treeItem.UpdateLayout();
            if (SelectFavoriteTreeItem(treeItem, id, kind))
            {
                return true;
            }
        }

        return false;
    }

    private string UniqueFolderName()
    {
        const string baseName = "New folder";
        if (_folders.All(folder => !string.Equals(folder.Name, baseName, StringComparison.CurrentCultureIgnoreCase)))
        {
            return baseName;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName} {index}";
            if (_folders.All(folder => !string.Equals(folder.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    private ConnectionFavoriteColor SelectedFavoriteColor()
    {
        return ProfileColorList.SelectedItem is FavoriteColorOption option
            ? option.Color
            : ConnectionFavoriteColor.None;
    }

    private FavoriteColorOption FavoriteColorOptionFor(ConnectionFavoriteColor color)
    {
        return _favoriteColorOptions.FirstOrDefault(option => option.Color == color) ?? _favoriteColorOptions[0];
    }

    private static ConnectionFavoriteColor ParseFavoriteColor(object? tag)
    {
        return int.TryParse(Convert.ToString(tag), out var value) &&
            Enum.IsDefined(typeof(ConnectionFavoriteColor), value)
            ? (ConnectionFavoriteColor)value
            : ConnectionFavoriteColor.None;
    }

    private Task SaveFavoritesAsync()
    {
        return _profileStore.SaveFavoritesAsync(_profiles, _folders);
    }

    private void LoadProfile(ConnectionProfile profile)
    {
        _loadingProfile = true;
        try
        {
            _selectedProfile = profile.Clone(includeSecrets: true);
            ProfileNameTextBox.Text = profile.Name;
            ProfileColorList.SelectedItem = FavoriteColorOptionFor(profile.Color);
            HostTextBox.Text = profile.Host;
            PortTextBox.Text = profile.Port.ToString();
            PipeNameTextBox.Text = profile.PipeName;
            UserNameTextBox.Text = profile.UserName;
            PasswordBox.Password = profile.Password;
            SavePasswordCheckBox.IsChecked = profile.SavePassword;
            DefaultSchemaTextBox.Text = profile.DefaultSchema;
            TlsModeCombo.SelectedItem = _tlsOptions.First(option => option.TlsMode == profile.TlsMode);
            SshHostTextBox.Text = profile.SshHost;
            SshPortTextBox.Text = profile.SshPort.ToString();
            SshUserNameTextBox.Text = profile.SshUserName;
            SshPasswordBox.Password = profile.SshPassword;
            SaveSshPasswordCheckBox.IsChecked = profile.SaveSshPassword;
            SshKeyPathTextBox.Text = profile.SshPrivateKeyPath;
            SshKeyPassphraseBox.Password = profile.SshPrivateKeyPassphrase;
            ProtocolCombo.SelectedItem = _protocolOptions.First(option => option.Protocol == profile.Protocol);
        }
        finally
        {
            _loadingProfile = false;
        }
    }

    private ConnectionProfile ReadProfileFromForm(bool includeSecrets)
    {
        var profile = _selectedProfile?.Clone(includeSecrets: true) ?? new ConnectionProfile();
        profile.Name = string.IsNullOrWhiteSpace(ProfileNameTextBox.Text) ? "New connection" : ProfileNameTextBox.Text.Trim();
        profile.Color = SelectedFavoriteColor();
        profile.Protocol = SelectedProtocol();
        profile.Host = HostTextBox.Text.Trim();
        profile.Port = ParseUInt(PortTextBox.Text, 3306);
        profile.PipeName = string.IsNullOrWhiteSpace(PipeNameTextBox.Text) ? "MYSQL" : PipeNameTextBox.Text.Trim();
        profile.UserName = UserNameTextBox.Text.Trim();
        profile.Password = includeSecrets ? PasswordBox.Password : "";
        profile.SavePassword = SavePasswordCheckBox.IsChecked == true;
        profile.DefaultSchema = DefaultSchemaTextBox.Text.Trim();
        profile.TlsMode = SelectedTlsMode();
        profile.SshHost = SshHostTextBox.Text.Trim();
        profile.SshPort = ParseUInt(SshPortTextBox.Text, 22);
        profile.SshUserName = SshUserNameTextBox.Text.Trim();
        profile.SshPassword = includeSecrets ? SshPasswordBox.Password : "";
        profile.SaveSshPassword = SaveSshPasswordCheckBox.IsChecked == true;
        profile.SshPrivateKeyPath = SshKeyPathTextBox.Text.Trim();
        profile.SshPrivateKeyPassphrase = includeSecrets ? SshKeyPassphraseBox.Password : "";
        return profile;
    }

    private ConnectionProtocolKind SelectedProtocol()
    {
        return ProtocolCombo.SelectedItem is ProtocolOption option
            ? option.Protocol
            : ConnectionProtocolKind.Tcp;
    }

    private DatabaseTlsMode SelectedTlsMode()
    {
        return TlsModeCombo.SelectedItem is TlsOption option
            ? option.TlsMode
            : DatabaseTlsMode.VerifyFull;
    }

    private static uint ParseUInt(string text, uint fallback)
    {
        return uint.TryParse(text, out var value) && value > 0 ? value : fallback;
    }

    private static string FindInitialPrivateKeyDirectory(string currentPath)
    {
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (Directory.Exists(currentPath))
            {
                return currentPath;
            }

            var parentDirectory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory) &&
                Directory.Exists(parentDirectory))
            {
                return parentDirectory;
            }
        }

        var sshDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh");
        return Directory.Exists(sshDirectory) ? sshDirectory : "";
    }

    private static void CopyProfile(ConnectionProfile source, ConnectionProfile target)
    {
        target.Name = source.Name;
        target.FolderId = source.FolderId;
        target.Color = source.Color;
        target.Protocol = source.Protocol;
        target.Host = source.Host;
        target.Port = source.Port;
        target.PipeName = source.PipeName;
        target.UserName = source.UserName;
        target.Password = source.Password;
        target.SavePassword = source.SavePassword;
        target.DefaultSchema = source.DefaultSchema;
        target.TlsMode = source.TlsMode;
        target.SshHost = source.SshHost;
        target.SshPort = source.SshPort;
        target.SshUserName = source.SshUserName;
        target.SshPassword = source.SshPassword;
        target.SaveSshPassword = source.SaveSshPassword;
        target.SshPrivateKeyPath = source.SshPrivateKeyPath;
        target.SshPrivateKeyPassphrase = source.SshPrivateKeyPassphrase;
    }

    private sealed record ProtocolOption(string Label, ConnectionProtocolKind Protocol)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record TlsOption(string Label, DatabaseTlsMode TlsMode)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record FavoriteColorOption(
        ConnectionFavoriteColor Color,
        string Label,
        Brush SwatchBrush,
        Brush BorderBrush)
    {
        public static FavoriteColorOption Create(ConnectionFavoriteColor color)
        {
            return new FavoriteColorOption(
                color,
                FavoriteColorPalette.LabelFor(color),
                FavoriteColorPalette.CreateSwatchBrush(color),
                color == ConnectionFavoriteColor.None ? Brushes.Gray : Brushes.Transparent);
        }

        public override string ToString()
        {
            return Label;
        }
    }
}
