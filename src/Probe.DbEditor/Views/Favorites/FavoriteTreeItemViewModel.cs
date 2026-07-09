using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Views.Favorites;

public sealed class FavoriteTreeItemViewModel : INotifyPropertyChanged
{
    private bool _isEditingName;
    private bool _isDragged;
    private bool _isExpanded = true;
    private FavoriteDropPreviewPlacement _dropPreviewPlacement;
    private string _dropPreviewName = "";

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

    public bool IsDragged
    {
        get => _isDragged;
        set
        {
            if (_isDragged == value)
            {
                return;
            }

            _isDragged = value;
            OnPropertyChanged();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public FavoriteDropPreviewPlacement DropPreviewPlacement
    {
        get => _dropPreviewPlacement;
        set
        {
            if (_dropPreviewPlacement == value)
            {
                return;
            }

            _dropPreviewPlacement = value;
            OnPropertyChanged();
        }
    }

    public string DropPreviewName
    {
        get => _dropPreviewName;
        set
        {
            if (string.Equals(_dropPreviewName, value, StringComparison.Ordinal))
            {
                return;
            }

            _dropPreviewName = value ?? "";
            OnPropertyChanged();
        }
    }

    public Brush BackgroundBrush => FavoriteColorPalette.CreateBackgroundBrush(Color, InheritedColor);

    public static FavoriteTreeItemViewModel ForFolder(ConnectionProfileFolder folder, bool isExpanded = true)
    {
        var item = new FavoriteTreeItemViewModel(
            FavoriteTreeItemKind.Folder,
            profile: null,
            folder,
            inheritedColor: ConnectionFavoriteColor.None);
        item.IsExpanded = isExpanded;
        return item;
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
