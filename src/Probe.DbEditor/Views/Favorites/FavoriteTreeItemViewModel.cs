using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Views.Favorites;

public sealed class FavoriteTreeItemViewModel : INotifyPropertyChanged
{
    private bool _isEditingName;
    private bool _isDropTarget;

    private FavoriteTreeItemViewModel(
        FavoriteTreeItemKind kind,
        ConnectionProfile? profile,
        ConnectionProfileFolder? folder,
        ConnectionFavoriteColor inheritedColor)
    {
        Kind = kind;
        Profile = profile;
        Folder = folder;
        InheritedColor = inheritedColor;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FavoriteTreeItemKind Kind { get; }

    public ConnectionProfile? Profile { get; }

    public ConnectionProfileFolder? Folder { get; }

    public ObservableCollection<FavoriteTreeItemViewModel> Children { get; } = [];

    public ConnectionFavoriteColor InheritedColor { get; }

    public string Id => Profile?.Id ?? Folder?.Id ?? "";

    public string Name
    {
        get => Profile?.Name ?? Folder?.Name ?? "";
        set
        {
            if (Profile is not null)
            {
                Profile.Name = value ?? "";
            }
            else if (Folder is not null)
            {
                Folder.Name = value ?? "";
            }

            OnPropertyChanged();
        }
    }

    public ConnectionFavoriteColor Color
    {
        get => Profile?.Color ?? Folder?.Color ?? ConnectionFavoriteColor.None;
        set
        {
            if (Profile is not null)
            {
                Profile.Color = value;
            }
            else if (Folder is not null)
            {
                Folder.Color = value;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundBrush));
        }
    }

    public bool IsFolder => Kind == FavoriteTreeItemKind.Folder;

    public bool IsProfile => Kind == FavoriteTreeItemKind.Profile;

    public bool IsEditingName
    {
        get => _isEditingName;
        set
        {
            if (_isEditingName == value)
            {
                return;
            }

            _isEditingName = value;
            OnPropertyChanged();
        }
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (_isDropTarget == value)
            {
                return;
            }

            _isDropTarget = value;
            OnPropertyChanged();
        }
    }

    public Brush BackgroundBrush => FavoriteColorPalette.CreateBackgroundBrush(Color, InheritedColor);

    public static FavoriteTreeItemViewModel ForFolder(ConnectionProfileFolder folder)
    {
        return new FavoriteTreeItemViewModel(
            FavoriteTreeItemKind.Folder,
            profile: null,
            folder,
            inheritedColor: ConnectionFavoriteColor.None);
    }

    public static FavoriteTreeItemViewModel ForProfile(
        ConnectionProfile profile,
        ConnectionFavoriteColor inheritedColor)
    {
        return new FavoriteTreeItemViewModel(
            FavoriteTreeItemKind.Profile,
            profile,
            folder: null,
            inheritedColor);
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
