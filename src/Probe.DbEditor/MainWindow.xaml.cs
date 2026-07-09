using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private const double FavoriteDropPreviewHitBuffer = 32;

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
    private FavoriteTreeItemViewModel? _favoriteDropTarget;
    private FavoriteTreeItemViewModel? _renamingFavoriteItem;
    private Point _favoriteDragStart;
    private FavoriteDropPreviewPlacement _favoriteDropPlacement;
    private string _favoriteDropPreviewName = "";
    private string _renameOriginalName = "";
    private bool _connectionOperationInProgress;
    private bool _favoriteDragArmed;
    private bool _isFavoriteDragging;
    private bool _isFavoriteRootDropTarget;
    private bool _loadingProfile;
    private ConnectionProfileFolder? _selectedFolder;

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
        FolderColorList.ItemsSource = _favoriteColorOptions;
        FolderColorList.SelectedItem = _favoriteColorOptions[0];

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

        await DeleteFavoriteAsync(item);
    }

    private void SavedProfilesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FavoriteTreeItemViewModel { Profile: not null } item)
        {
            LoadProfile(item.Profile);
        }
        else if (e.NewValue is FavoriteTreeItemViewModel { Folder: not null } folderItem)
        {
            LoadFolder(folderItem.Folder);
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
        ClearFavoriteDragCandidate();
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
        ClearFavoriteDragCandidate();
        _contextFavoriteItem = (
            FindTreeViewItem(e.OriginalSource) ??
            FindTreeViewItem(Mouse.DirectlyOver))?.DataContext as FavoriteTreeItemViewModel;

        FavoriteContextColorMenu.IsEnabled = _contextFavoriteItem is not null;
        FavoriteContextConnectMenuItem.IsEnabled = _contextFavoriteItem?.Profile is not null;
        FavoriteContextDuplicateMenuItem.IsEnabled = _contextFavoriteItem?.Profile is not null;
        FavoriteContextRenameMenuItem.IsEnabled = _contextFavoriteItem is not null;
        FavoriteContextDeleteMenuItem.IsEnabled = _contextFavoriteItem is not null;

        foreach (var colorMenuItem in FavoriteContextColorMenu.Items.OfType<MenuItem>())
        {
            colorMenuItem.IsCheckable = true;
            colorMenuItem.IsChecked = ParseFavoriteColor(colorMenuItem.Tag) == _contextFavoriteItem?.Color;
        }
    }

    private void SavedProfilesContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        ClearFavoriteDragCandidate();
    }

    private void FavoriteContextNewProfile_Click(object sender, RoutedEventArgs e)
    {
        LoadProfile(new ConnectionProfile { FolderId = ContextFavoriteFolderId() });
    }

    private async void FavoriteContextNewFolder_Click(object sender, RoutedEventArgs e)
    {
        await AddFolderAsync();
    }

    private async void FavoriteContextConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_contextFavoriteItem?.Profile is not null)
        {
            LoadProfile(_contextFavoriteItem.Profile);
            await ConnectCurrentProfileAsync();
        }
    }

    private async void FavoriteContextDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_contextFavoriteItem?.Profile is not null)
        {
            await DuplicateProfileAsync(_contextFavoriteItem.Profile);
        }
    }

    private async void FavoriteContextDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_contextFavoriteItem is not null)
        {
            await DeleteFavoriteAsync(_contextFavoriteItem);
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
        else if (_contextFavoriteItem.Folder is not null &&
                 _selectedFolder?.Id == _contextFavoriteItem.Folder.Id)
        {
            FolderColorList.SelectedItem = FavoriteColorOptionFor(color);
        }

        var selectedId = _contextFavoriteItem.Id;
        var selectedKind = _contextFavoriteItem.Kind;
        await SaveFavoritesAsync();
        RefreshFavoriteTree(selectedId, selectedKind);
    }

    private void SavedProfilesTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ClearFavoriteDragCandidate();
        if (SavedProfilesContextMenu.IsOpen)
        {
            return;
        }

        var treeItem = FindTreeViewItem(e.OriginalSource);
        if (treeItem?.DataContext is FavoriteTreeItemViewModel { Folder: not null } folderItem &&
            FindVisualAncestor<ToggleButton>(e.OriginalSource) is null &&
            FindVisualAncestor<TextBox>(e.OriginalSource) is null)
        {
            treeItem.IsExpanded = true;
            folderItem.IsExpanded = true;
        }

        if (treeItem?.DataContext is not FavoriteTreeItemViewModel item ||
            (item.Profile is null && item.Folder is null))
        {
            return;
        }

        if (item.IsEditingName)
        {
            return;
        }

        _favoriteDragStart = e.GetPosition(SavedProfilesTree);
        _favoriteDragCandidate = item;
        _favoriteDragArmed = true;
    }

    private void SavedProfilesTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ClearFavoriteDragCandidate();
    }

    private void SavedProfilesTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_favoriteDragArmed ||
            e.LeftButton != MouseButtonState.Pressed ||
            _favoriteDragCandidate is null ||
            (_favoriteDragCandidate.Profile is null && _favoriteDragCandidate.Folder is null))
        {
            return;
        }

        var currentPosition = e.GetPosition(SavedProfilesTree);
        if (Math.Abs(currentPosition.X - _favoriteDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _favoriteDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(
            typeof(FavoriteDragPayload),
            new FavoriteDragPayload(_favoriteDragCandidate.Kind, _favoriteDragCandidate.Id));
        try
        {
            StartFavoriteDragPreview(_favoriteDragCandidate);
            DragDrop.DoDragDrop(SavedProfilesTree, data, DragDropEffects.Move);
        }
        finally
        {
            StopFavoriteDragPreview();
            ClearFavoriteDropTarget();
            ClearFavoriteDragCandidate();
        }
    }

    private void SavedProfilesTree_DragOver(object sender, DragEventArgs e)
    {
        UpdateFavoriteDragPreviewPosition();
        if (ResolveFavoriteDropTarget(e) is { } dropTarget)
        {
            e.Effects = DragDropEffects.Move;
            SetFavoriteDropTarget(dropTarget);
        }
        else
        {
            e.Effects = DragDropEffects.None;
            ClearFavoriteDropTarget();
        }

        e.Handled = true;
    }

    private async void SavedProfilesTree_Drop(object sender, DragEventArgs e)
    {
        var dropTarget = ResolveFavoriteDropTarget(e);
        if (dropTarget is null)
        {
            ClearFavoriteDropTarget();
            return;
        }

        MoveFavoriteToDropTarget(dropTarget);
        if (dropTarget.Profile is not null &&
            _selectedProfile?.Id == dropTarget.Profile.Id)
        {
            _selectedProfile.FolderId = dropTarget.TargetFolderId;
        }

        await SaveFavoritesAsync();
        RefreshFavoriteTree(dropTarget.DraggedId, dropTarget.Kind);
        ClearFavoriteDropTarget();
        e.Handled = true;
    }

    private void SavedProfilesTree_DragLeave(object sender, DragEventArgs e)
    {
        if (!SavedProfilesTree.IsMouseOver)
        {
            ClearFavoriteDropTarget();
        }
    }

    private void SavedProfilesTree_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (!_isFavoriteDragging)
        {
            return;
        }

        UpdateFavoriteDragPreviewPosition();
        Mouse.SetCursor(e.Effects == DragDropEffects.Move ? Cursors.Hand : Cursors.No);
        e.UseDefaultCursors = false;
        e.Handled = true;
    }

    private void SavedProfilesTree_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed || e.KeyStates.HasFlag(DragDropKeyStates.LeftMouseButton) == false)
        {
            ClearFavoriteDropTarget();
        }
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

    private async void SaveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolder is null)
        {
            return;
        }

        _selectedFolder.Name = string.IsNullOrWhiteSpace(FolderNameTextBox.Text)
            ? "New folder"
            : FolderNameTextBox.Text.Trim();
        _selectedFolder.Color = SelectedFolderColor();

        await SaveFavoritesAsync();
        RefreshFavoriteTree(_selectedFolder.Id, FavoriteTreeItemKind.Folder);
        ConnectionStatusText.Text = "Folder saved.";
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
                var tab = new TabItem
                {
                    Content = view,
                    Padding = new Thickness(0)
                };
                tab.Header = CreateConnectionTabHeader(profile, tab);

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

    private Border CreateConnectionTabHeader(ConnectionProfile profile, TabItem tab)
    {
        var header = new Border
        {
            Background = CreateConnectionTabHeaderBrush(profile),
            Padding = new Thickness(12, 6, 12, 6),
            Tag = tab
        };
        header.PreviewMouseDown += ConnectionTabHeader_PreviewMouseDown;

        var panel = new DockPanel
        {
            LastChildFill = false
        };

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
            Text = profile.Name,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(closeButton);
        header.Child = panel;
        return header;
    }

    private Brush CreateConnectionTabHeaderBrush(ConnectionProfile profile)
    {
        var inheritedColor = _folders
            .FirstOrDefault(folder => string.Equals(folder.Id, profile.FolderId, StringComparison.Ordinal))
            ?.Color ?? ConnectionFavoriteColor.None;
        return FavoriteColorPalette.CreateBackgroundBrush(profile.Color, inheritedColor);
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
        var expandedFolderIds = CaptureExpandedFolderIds();
        var hasExpansionState = _favoriteTree.Any(item => item.Folder is not null);
        _favoriteTree.Clear();

        var foldersById = _folders.ToDictionary(folder => folder.Id, StringComparer.Ordinal);
        foreach (var folder in _folders)
        {
            var isExpanded = !hasExpansionState ||
                             expandedFolderIds.Contains(folder.Id) ||
                             (selectedKind == FavoriteTreeItemKind.Folder &&
                              string.Equals(selectedId, folder.Id, StringComparison.Ordinal));
            var folderItem = FavoriteTreeItemViewModel.ForFolder(folder, isExpanded);
            foreach (var profile in _profiles
                .Where(profile => string.Equals(profile.FolderId, folder.Id, StringComparison.Ordinal)))
            {
                folderItem.Children.Add(FavoriteTreeItemViewModel.ForProfile(profile, folder.Color));
            }

            _favoriteTree.Add(folderItem);
        }

        foreach (var profile in _profiles
            .Where(profile => string.IsNullOrWhiteSpace(profile.FolderId) || !foldersById.ContainsKey(profile.FolderId)))
        {
            profile.FolderId = "";
            _favoriteTree.Add(FavoriteTreeItemViewModel.ForProfile(profile, ConnectionFavoriteColor.None));
        }

        if (!string.IsNullOrWhiteSpace(selectedId) && selectedKind is not null)
        {
            Dispatcher.BeginInvoke(() => SelectFavoriteTreeItem(selectedId, selectedKind.Value));
        }
    }

    private HashSet<string> CaptureExpandedFolderIds()
    {
        return _favoriteTree
            .Where(item => item.Folder is not null && item.IsExpanded)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
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

    private async Task DuplicateProfileAsync(ConnectionProfile profile)
    {
        var duplicate = profile.Clone(includeSecrets: true);
        duplicate.Id = Guid.NewGuid().ToString("N");
        duplicate.Name = UniqueProfileName($"{profile.Name} copy", profile.FolderId);

        var sourceIndex = _profiles.IndexOf(profile);
        if (sourceIndex >= 0)
        {
            _profiles.Insert(sourceIndex + 1, duplicate);
        }
        else
        {
            _profiles.Add(duplicate);
        }

        await SaveFavoritesAsync();
        RefreshFavoriteTree(duplicate.Id, FavoriteTreeItemKind.Profile);
        LoadProfile(duplicate);
        _ = Dispatcher.BeginInvoke(() =>
        {
            ProfileNameTextBox.Focus();
            ProfileNameTextBox.SelectAll();
        });
    }

    private async Task DeleteFavoriteAsync(FavoriteTreeItemViewModel item)
    {
        if (!ConfirmDeleteFavorite(item))
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

    private bool ConfirmDeleteFavorite(FavoriteTreeItemViewModel item)
    {
        var title = item.Profile is not null ? "Delete Profile" : "Delete Folder";
        var message = item.Profile is not null
            ? $"Delete profile \"{item.Name}\"?"
            : $"Delete folder \"{item.Name}\" and all contained profiles?";
        return MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
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
        if ((_selectedProfile is not null && deletedProfileIds.Contains(_selectedProfile.Id)) ||
            string.Equals(_selectedFolder?.Id, folder.Id, StringComparison.Ordinal))
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

    private FavoriteDropTarget? ResolveFavoriteDropTarget(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(FavoriteDragPayload)) ||
            e.Data.GetData(typeof(FavoriteDragPayload)) is not FavoriteDragPayload payload)
        {
            return null;
        }

        return payload.Kind switch
        {
            FavoriteTreeItemKind.Profile => _profiles.FirstOrDefault(profile => string.Equals(profile.Id, payload.Id, StringComparison.Ordinal)) is { } profile
                ? ResolveProfileDropTarget(e, profile)
                : null,
            FavoriteTreeItemKind.Folder => _folders.FirstOrDefault(folder => string.Equals(folder.Id, payload.Id, StringComparison.Ordinal)) is { } folder
                ? ResolveFolderDropTarget(e, folder)
                : null,
            _ => null
        };
    }

    private FavoriteDropTarget? ResolveProfileDropTarget(DragEventArgs e, ConnectionProfile droppedProfile)
    {
        if (TryResolveCachedProfileDropTarget(e, droppedProfile, out var cachedDropTarget))
        {
            return cachedDropTarget;
        }

        var targetTreeItem = FindTreeViewItem(e.OriginalSource);
        var targetItem = targetTreeItem?.DataContext as FavoriteTreeItemViewModel;
        if (targetItem?.Profile is { } targetProfile)
        {
            var placement = ResolveFavoriteDropPlacement(e, targetTreeItem!, targetItem);
            return CreateProfileDropTarget(droppedProfile, targetItem, placement);
        }

        if (targetItem?.Folder is { } targetFolder)
        {
            targetTreeItem!.IsExpanded = true;
            targetItem.IsExpanded = true;
            var targetIndex = ProfilesInFolder(targetFolder.Id)
                .Count(profile => !string.Equals(profile.Id, droppedProfile.Id, StringComparison.Ordinal));
            if (!IsEffectiveProfileMove(droppedProfile, targetFolder.Id, targetIndex))
            {
                return null;
            }

            return new FavoriteDropTarget(
                FavoriteTreeItemKind.Profile,
                droppedProfile.Id,
                droppedProfile.Name,
                Profile: droppedProfile,
                Folder: null,
                targetFolder.Id,
                targetIndex,
                targetItem,
                FavoriteDropPreviewPlacement.Inside);
        }

        var rootTargetIndex = ProfilesInFolder("")
            .Count(profile => !string.Equals(profile.Id, droppedProfile.Id, StringComparison.Ordinal));
        return IsEffectiveProfileMove(droppedProfile, "", rootTargetIndex)
            ? new FavoriteDropTarget(
                FavoriteTreeItemKind.Profile,
                droppedProfile.Id,
                droppedProfile.Name,
                Profile: droppedProfile,
                Folder: null,
                "",
                rootTargetIndex,
                PreviewItem: null,
                FavoriteDropPreviewPlacement.None)
            : null;
    }

    private bool TryResolveCachedProfileDropTarget(
        DragEventArgs e,
        ConnectionProfile droppedProfile,
        out FavoriteDropTarget? dropTarget)
    {
        dropTarget = null;
        if (_favoriteDropTarget?.Profile is null ||
            _favoriteDropPlacement is not (FavoriteDropPreviewPlacement.Before or FavoriteDropPreviewPlacement.After) ||
            string.Equals(_favoriteDropTarget.Profile.Id, droppedProfile.Id, StringComparison.Ordinal) ||
            !IsPointerWithinCachedDropPreviewBand(e, _favoriteDropTarget, _favoriteDropPlacement))
        {
            return false;
        }

        dropTarget = CreateProfileDropTarget(droppedProfile, _favoriteDropTarget, _favoriteDropPlacement);
        return dropTarget is not null;
    }

    private FavoriteDropTarget? CreateProfileDropTarget(
        ConnectionProfile droppedProfile,
        FavoriteTreeItemViewModel targetItem,
        FavoriteDropPreviewPlacement placement)
    {
        if (targetItem.Profile is not { } targetProfile ||
            string.Equals(targetProfile.Id, droppedProfile.Id, StringComparison.Ordinal))
        {
            return null;
        }

        var targetFolderId = targetProfile.FolderId;
        var siblings = ProfilesInFolder(targetFolderId)
            .Where(profile => !string.Equals(profile.Id, droppedProfile.Id, StringComparison.Ordinal))
            .ToList();
        var siblingIndex = siblings.FindIndex(profile => string.Equals(profile.Id, targetProfile.Id, StringComparison.Ordinal));
        if (siblingIndex < 0)
        {
            return null;
        }

        var targetIndex = siblingIndex + (placement == FavoriteDropPreviewPlacement.After ? 1 : 0);
        return IsEffectiveProfileMove(droppedProfile, targetFolderId, targetIndex)
            ? new FavoriteDropTarget(
                FavoriteTreeItemKind.Profile,
                droppedProfile.Id,
                droppedProfile.Name,
                Profile: droppedProfile,
                Folder: null,
                targetFolderId,
                targetIndex,
                targetItem,
                placement)
            : null;
    }

    private FavoriteDropTarget? ResolveFolderDropTarget(DragEventArgs e, ConnectionProfileFolder droppedFolder)
    {
        if (TryResolveCachedFolderDropTarget(e, droppedFolder, out var cachedDropTarget))
        {
            return cachedDropTarget;
        }

        var targetTreeItem = FindTreeViewItem(e.OriginalSource);
        var targetItem = targetTreeItem?.DataContext as FavoriteTreeItemViewModel;
        if (targetItem?.Folder is not null)
        {
            var placement = ResolveFavoriteDropPlacement(e, targetTreeItem!, targetItem);
            return CreateFolderDropTarget(droppedFolder, targetItem, placement);
        }

        if (targetTreeItem is not null)
        {
            return null;
        }

        var rootTargetIndex = _folders.Count(folder => !string.Equals(folder.Id, droppedFolder.Id, StringComparison.Ordinal));
        return IsEffectiveFolderMove(droppedFolder, rootTargetIndex)
            ? new FavoriteDropTarget(
                FavoriteTreeItemKind.Folder,
                droppedFolder.Id,
                droppedFolder.Name,
                Profile: null,
                Folder: droppedFolder,
                TargetFolderId: "",
                rootTargetIndex,
                PreviewItem: null,
                FavoriteDropPreviewPlacement.None)
            : null;
    }

    private bool TryResolveCachedFolderDropTarget(
        DragEventArgs e,
        ConnectionProfileFolder droppedFolder,
        out FavoriteDropTarget? dropTarget)
    {
        dropTarget = null;
        if (_favoriteDropTarget?.Folder is null ||
            _favoriteDropPlacement is not (FavoriteDropPreviewPlacement.Before or FavoriteDropPreviewPlacement.After) ||
            string.Equals(_favoriteDropTarget.Folder.Id, droppedFolder.Id, StringComparison.Ordinal) ||
            !IsPointerWithinCachedDropPreviewBand(e, _favoriteDropTarget, _favoriteDropPlacement))
        {
            return false;
        }

        dropTarget = CreateFolderDropTarget(droppedFolder, _favoriteDropTarget, _favoriteDropPlacement);
        return dropTarget is not null;
    }

    private FavoriteDropTarget? CreateFolderDropTarget(
        ConnectionProfileFolder droppedFolder,
        FavoriteTreeItemViewModel targetItem,
        FavoriteDropPreviewPlacement placement)
    {
        if (targetItem.Folder is not { } targetFolder ||
            string.Equals(targetFolder.Id, droppedFolder.Id, StringComparison.Ordinal))
        {
            return null;
        }

        var siblings = _folders
            .Where(folder => !string.Equals(folder.Id, droppedFolder.Id, StringComparison.Ordinal))
            .ToList();
        var siblingIndex = siblings.FindIndex(folder => string.Equals(folder.Id, targetFolder.Id, StringComparison.Ordinal));
        if (siblingIndex < 0)
        {
            return null;
        }

        var targetIndex = siblingIndex + (placement == FavoriteDropPreviewPlacement.After ? 1 : 0);
        return IsEffectiveFolderMove(droppedFolder, targetIndex)
            ? new FavoriteDropTarget(
                FavoriteTreeItemKind.Folder,
                droppedFolder.Id,
                droppedFolder.Name,
                Profile: null,
                Folder: droppedFolder,
                TargetFolderId: "",
                targetIndex,
                targetItem,
                placement)
            : null;
    }

    private FavoriteDropPreviewPlacement ResolveFavoriteDropPlacement(
        DragEventArgs e,
        TreeViewItem targetTreeItem,
        FavoriteTreeItemViewModel targetItem)
    {
        var itemRow = FindVisualDescendantByName<FrameworkElement>(targetTreeItem, "ItemRow");
        if (itemRow is not null)
        {
            var rowPosition = e.GetPosition(itemRow);
            if (ReferenceEquals(_favoriteDropTarget, targetItem) &&
                _favoriteDropPlacement is FavoriteDropPreviewPlacement.Before or FavoriteDropPreviewPlacement.After &&
                (rowPosition.Y < 0 || rowPosition.Y > itemRow.ActualHeight))
            {
                return _favoriteDropPlacement;
            }

            var rowHeight = Math.Max(1, itemRow.ActualHeight);
            return rowPosition.Y <= rowHeight / 2
                ? FavoriteDropPreviewPlacement.Before
                : FavoriteDropPreviewPlacement.After;
        }

        var targetHeight = Math.Max(1, targetTreeItem.ActualHeight);
        return e.GetPosition(targetTreeItem).Y <= targetHeight / 2
            ? FavoriteDropPreviewPlacement.Before
            : FavoriteDropPreviewPlacement.After;
    }

    private void MoveFavoriteToDropTarget(FavoriteDropTarget dropTarget)
    {
        if (dropTarget.Profile is not null)
        {
            MoveProfileToDropTarget(dropTarget);
        }
        else if (dropTarget.Folder is not null)
        {
            MoveFolderToDropTarget(dropTarget);
        }
    }

    private void MoveProfileToDropTarget(FavoriteDropTarget dropTarget)
    {
        if (dropTarget.Profile is null)
        {
            return;
        }

        _profiles.Remove(dropTarget.Profile);
        dropTarget.Profile.FolderId = dropTarget.TargetFolderId;

        var siblings = ProfilesInFolder(dropTarget.TargetFolderId)
            .Where(profile => !string.Equals(profile.Id, dropTarget.Profile.Id, StringComparison.Ordinal))
            .ToList();
        var targetIndex = Math.Clamp(dropTarget.TargetIndex, 0, siblings.Count);
        var insertIndex = _profiles.Count;
        if (targetIndex < siblings.Count)
        {
            insertIndex = _profiles.IndexOf(siblings[targetIndex]);
        }
        else if (siblings.Count > 0)
        {
            insertIndex = _profiles.IndexOf(siblings[^1]) + 1;
        }

        _profiles.Insert(insertIndex, dropTarget.Profile);
    }

    private void MoveFolderToDropTarget(FavoriteDropTarget dropTarget)
    {
        if (dropTarget.Folder is null)
        {
            return;
        }

        _folders.Remove(dropTarget.Folder);
        var siblings = _folders
            .Where(folder => !string.Equals(folder.Id, dropTarget.Folder.Id, StringComparison.Ordinal))
            .ToList();
        var targetIndex = Math.Clamp(dropTarget.TargetIndex, 0, siblings.Count);
        var insertIndex = _folders.Count;
        if (targetIndex < siblings.Count)
        {
            insertIndex = _folders.IndexOf(siblings[targetIndex]);
        }
        else if (siblings.Count > 0)
        {
            insertIndex = _folders.IndexOf(siblings[^1]) + 1;
        }

        _folders.Insert(insertIndex, dropTarget.Folder);
    }

    private bool IsEffectiveProfileMove(
        ConnectionProfile profile,
        string targetFolderId,
        int targetIndex)
    {
        if (!FavoriteFolderIdsMatch(profile.FolderId, targetFolderId))
        {
            return true;
        }

        var siblings = ProfilesInFolder(targetFolderId).ToList();
        var currentIndex = siblings.FindIndex(candidate => string.Equals(candidate.Id, profile.Id, StringComparison.Ordinal));
        return currentIndex >= 0 && currentIndex != targetIndex;
    }

    private bool IsEffectiveFolderMove(
        ConnectionProfileFolder folder,
        int targetIndex)
    {
        var siblings = _folders.ToList();
        var currentIndex = siblings.FindIndex(candidate => string.Equals(candidate.Id, folder.Id, StringComparison.Ordinal));
        return currentIndex >= 0 && currentIndex != targetIndex;
    }

    private IEnumerable<ConnectionProfile> ProfilesInFolder(string folderId)
    {
        return _profiles.Where(profile => FavoriteFolderIdsMatch(profile.FolderId, folderId));
    }

    private static bool FavoriteFolderIdsMatch(string profileFolderId, string folderId)
    {
        return string.IsNullOrWhiteSpace(folderId)
            ? string.IsNullOrWhiteSpace(profileFolderId)
            : string.Equals(profileFolderId, folderId, StringComparison.Ordinal);
    }

    private void ClearFavoriteDragCandidate()
    {
        _favoriteDragArmed = false;
        if (_favoriteDragCandidate is not null)
        {
            _favoriteDragCandidate.IsDragged = false;
        }

        _favoriteDragCandidate = null;
    }

    private void StartFavoriteDragPreview(FavoriteTreeItemViewModel item)
    {
        _isFavoriteDragging = true;
        item.IsDragged = true;
        FavoriteDragPreviewText.Text = item.Name;
        FavoriteDragPreviewPopup.IsOpen = true;
        UpdateFavoriteDragPreviewPosition();
    }

    private void StopFavoriteDragPreview()
    {
        _isFavoriteDragging = false;
        FavoriteDragPreviewPopup.IsOpen = false;
        Mouse.OverrideCursor = null;
    }

    private void UpdateFavoriteDragPreviewPosition()
    {
        if (!_isFavoriteDragging)
        {
            return;
        }

        var position = Mouse.GetPosition(SavedProfilesTree);
        FavoriteDragPreviewPopup.HorizontalOffset = position.X + 14;
        FavoriteDragPreviewPopup.VerticalOffset = position.Y + 14;
    }

    private void SetFavoriteDropTarget(FavoriteDropTarget dropTarget)
    {
        var isRootTarget = dropTarget.PreviewItem is null;
        if (ReferenceEquals(_favoriteDropTarget, dropTarget.PreviewItem) &&
            _favoriteDropPlacement == dropTarget.Placement &&
            _isFavoriteRootDropTarget == isRootTarget &&
            string.Equals(_favoriteDropPreviewName, dropTarget.DraggedName, StringComparison.Ordinal))
        {
            return;
        }

        ClearFavoriteDropTarget();
        _favoriteDropPlacement = dropTarget.Placement;
        _favoriteDropPreviewName = dropTarget.DraggedName;
        _isFavoriteRootDropTarget = isRootTarget;
        if (dropTarget.PreviewItem is not null)
        {
            _favoriteDropTarget = dropTarget.PreviewItem;
            _favoriteDropTarget.DropPreviewName = dropTarget.DraggedName;
            _favoriteDropTarget.DropPreviewPlacement = dropTarget.Placement;
            FavoriteRootDropPreview.Visibility = Visibility.Collapsed;
            FavoriteRootDropPreviewText.Text = "";
            return;
        }

        FavoriteRootDropPreviewText.Text = _favoriteDropPreviewName;
        FavoriteRootDropPreview.Visibility = Visibility.Visible;
    }

    private void ClearFavoriteDropTarget()
    {
        if (_favoriteDropTarget is not null)
        {
            _favoriteDropTarget.DropPreviewPlacement = FavoriteDropPreviewPlacement.None;
            _favoriteDropTarget.DropPreviewName = "";
            _favoriteDropTarget = null;
        }

        FavoriteRootDropPreview.Visibility = Visibility.Collapsed;
        FavoriteRootDropPreviewText.Text = "";
        _favoriteDropPlacement = FavoriteDropPreviewPlacement.None;
        _favoriteDropPreviewName = "";
        _isFavoriteRootDropTarget = false;
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

    private bool IsPointerWithinCachedDropPreviewBand(
        DragEventArgs e,
        FavoriteTreeItemViewModel item,
        FavoriteDropPreviewPlacement placement)
    {
        var treeItem = FindFavoriteTreeViewItem(SavedProfilesTree, item);
        if (treeItem is null)
        {
            return false;
        }

        var itemRow = FindVisualDescendantByName<FrameworkElement>(treeItem, "ItemRow");
        if (itemRow is null)
        {
            var position = e.GetPosition(treeItem);
            return position.X >= 0 &&
                   position.X <= treeItem.ActualWidth &&
                   position.Y >= -FavoriteDropPreviewHitBuffer &&
                   position.Y <= treeItem.ActualHeight + FavoriteDropPreviewHitBuffer;
        }

        var rowPosition = e.GetPosition(itemRow);
        if (rowPosition.X < 0 ||
            rowPosition.X > itemRow.ActualWidth)
        {
            return false;
        }

        return placement switch
        {
            FavoriteDropPreviewPlacement.Before => rowPosition.Y < 0 &&
                                                   rowPosition.Y >= -FavoriteDropPreviewHitBuffer,
            FavoriteDropPreviewPlacement.After => rowPosition.Y > itemRow.ActualHeight &&
                                                  rowPosition.Y <= itemRow.ActualHeight + FavoriteDropPreviewHitBuffer,
            _ => false
        };
    }

    private static TreeViewItem? FindFavoriteTreeViewItem(
        ItemsControl parent,
        FavoriteTreeItemViewModel target)
    {
        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeItem)
            {
                continue;
            }

            if (ReferenceEquals(item, target))
            {
                return treeItem;
            }

            var nested = FindFavoriteTreeViewItem(treeItem, target);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindVisualAncestor<T>(object? originalSource)
        where T : DependencyObject
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindVisualDescendantByName<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            var nested = FindVisualDescendantByName<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
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

            var wasExpanded = treeItem.IsExpanded;
            treeItem.IsExpanded = true;
            treeItem.UpdateLayout();
            if (SelectFavoriteTreeItem(treeItem, id, kind))
            {
                return true;
            }

            treeItem.IsExpanded = wasExpanded;
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

    private string UniqueProfileName(string baseName, string folderId)
    {
        if (_profiles
            .Where(profile => FavoriteFolderIdsMatch(profile.FolderId, folderId))
            .All(profile => !string.Equals(profile.Name, baseName, StringComparison.CurrentCultureIgnoreCase)))
        {
            return baseName;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName} {index}";
            if (_profiles
                .Where(profile => FavoriteFolderIdsMatch(profile.FolderId, folderId))
                .All(profile => !string.Equals(profile.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
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

    private ConnectionFavoriteColor SelectedFolderColor()
    {
        return FolderColorList.SelectedItem is FavoriteColorOption option
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
            _selectedFolder = null;
            _selectedProfile = profile.Clone(includeSecrets: true);
            FolderDetailPanel.Visibility = Visibility.Collapsed;
            ProfileDetailPanel.Visibility = Visibility.Visible;
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

    private void LoadFolder(ConnectionProfileFolder folder)
    {
        _selectedProfile = null;
        _selectedFolder = folder;
        ProfileDetailPanel.Visibility = Visibility.Collapsed;
        FolderDetailPanel.Visibility = Visibility.Visible;
        FolderNameTextBox.Text = folder.Name;
        FolderColorList.SelectedItem = FavoriteColorOptionFor(folder.Color);
        ConnectionStatusText.Text = "";
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

    private sealed record FavoriteDragPayload(
        FavoriteTreeItemKind Kind,
        string Id);

    private sealed record FavoriteDropTarget(
        FavoriteTreeItemKind Kind,
        string DraggedId,
        string DraggedName,
        ConnectionProfile? Profile,
        ConnectionProfileFolder? Folder,
        string TargetFolderId,
        int TargetIndex,
        FavoriteTreeItemViewModel? PreviewItem,
        FavoriteDropPreviewPlacement Placement);
}
