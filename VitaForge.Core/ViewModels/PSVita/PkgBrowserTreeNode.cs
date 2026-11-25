using System.Collections.ObjectModel;
using VitaForge.Core.ViewModels;

namespace VitaForge.Core.ViewModels.PSVita;

public sealed class PkgBrowserTreeNode(string name, string fullPath, Action<PkgBrowserTreeNode> onSelected) : ViewModelBase
{
    public string Name { get; } = name;

    public string FullPath { get; } = fullPath;

    public ObservableCollection<PkgBrowserTreeNode> Children { get; } = [];

    public bool IsExpanded { get; set { SetProperty(ref field, value); } }

    public bool IsSelected
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && value)
                onSelected(this);
        }
    }
}