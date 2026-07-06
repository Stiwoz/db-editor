using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Probe.DbEditor.Models;
using Probe.DbEditor.Services;
using Probe.DbEditor.Themes;
using Probe.DbEditor.Views;

namespace Probe.DbEditor;

public partial class MainWindow : Window
{
    private readonly ConnectionProfileStore _profileStore = new();
    private readonly ObservableCollection<ConnectionProfile> _profiles = [];
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

    private ConnectionProfile? _selectedProfile;

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

        SavedProfilesList.ItemsSource = _profiles;
        foreach (var profile in await _profileStore.LoadAsync())
        {
            _profiles.Add(profile);
        }

        LoadProfile(new ConnectionProfile());
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        SavedProfilesList.SelectedItem = null;
        LoadProfile(new ConnectionProfile());
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfilesList.SelectedItem is not ConnectionProfile profile)
        {
            return;
        }

        _profiles.Remove(profile);
        await _profileStore.SaveAsync(_profiles);
        SavedProfilesList.SelectedItem = null;
        LoadProfile(new ConnectionProfile());
    }

    private void SavedProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SavedProfilesList.SelectedItem is ConnectionProfile profile)
        {
            LoadProfile(profile);
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
                SavedProfilesList.SelectedItem = profile;
            }
            else
            {
                CopyProfile(profile, existing);
                SavedProfilesList.Items.Refresh();
            }

            await _profileStore.SaveAsync(_profiles);
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
        try
        {
            ConnectionStatusText.Text = "Connecting...";
            var profile = ReadProfileFromForm(includeSecrets: true);
            var session = new DatabaseSession(profile);
            await session.OpenAsync();

            var view = new SessionView(session);
            var tab = new TabItem
            {
                Header = profile.Name,
                Content = view
            };

            ConnectionsTab.Items.Add(tab);
            ConnectionsTab.SelectedItem = tab;
            ConnectionStatusText.Text = "Connected.";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = ex.Message;
        }
    }

    private void LoadProfile(ConnectionProfile profile)
    {
        _selectedProfile = profile.Clone(includeSecrets: true);
        ProfileNameTextBox.Text = profile.Name;
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
        SshHostKeyFingerprintTextBox.Text = profile.SshHostKeyFingerprint;
        SshDatabaseHostTextBox.Text = profile.SshDatabaseHost;
        SshDatabasePortTextBox.Text = profile.SshDatabasePort.ToString();
        ProtocolCombo.SelectedItem = _protocolOptions.First(option => option.Protocol == profile.Protocol);
    }

    private ConnectionProfile ReadProfileFromForm(bool includeSecrets)
    {
        var profile = _selectedProfile?.Clone(includeSecrets: true) ?? new ConnectionProfile();
        profile.Name = string.IsNullOrWhiteSpace(ProfileNameTextBox.Text) ? "New connection" : ProfileNameTextBox.Text.Trim();
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
        profile.SshHostKeyFingerprint = SshHostKeyFingerprintTextBox.Text.Trim();
        profile.SshDatabaseHost = string.IsNullOrWhiteSpace(SshDatabaseHostTextBox.Text) ? "127.0.0.1" : SshDatabaseHostTextBox.Text.Trim();
        profile.SshDatabasePort = ParseUInt(SshDatabasePortTextBox.Text, 3306);
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

    private static void CopyProfile(ConnectionProfile source, ConnectionProfile target)
    {
        target.Name = source.Name;
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
        target.SshHostKeyFingerprint = source.SshHostKeyFingerprint;
        target.SshDatabaseHost = source.SshDatabaseHost;
        target.SshDatabasePort = source.SshDatabasePort;
    }

    private sealed record ProtocolOption(string Label, ConnectionProtocolKind Protocol);

    private sealed record TlsOption(string Label, DatabaseTlsMode TlsMode);
}
