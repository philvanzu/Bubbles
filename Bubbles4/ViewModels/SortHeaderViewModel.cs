using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Media;
using Bubbles4.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public class FullSortHeaderViewModel:SortHeaderViewModel<LibraryConfig.SortOptions>{}
public class ShortSortHeaderViewModel:SortHeaderViewModel<LibraryConfig.NodeSortOptions>{}
public partial class SortHeaderViewModel<TEnum> : ViewModelBase
    where TEnum : struct, Enum
{
    public List<SortHeaderOption<TEnum>> OptionsEnum { get; } = new();
    private bool _noChangeState;
    //updated by the main viewmodel
    public ( TEnum sortOption, bool ascending) Value
    {
        get => ( Selected.SortOption, Selected.IsAscending);
        set
        {
            _noChangeState = true;
            foreach (var option in Options)
            {
                if (EqualityComparer<TEnum>.Default.Equals(option.SortOption, value.sortOption))
                {
                    option.IsSelected = true;
                    option.IsAscending = value.ascending;
                    break;
                }
            }
            _noChangeState = false;
            OnStateChanged();
        }
    }
    
    //updated by the SortHeaders collection
    [ObservableProperty] private List<SortHeaderOption<TEnum>> _options = new();
    [ObservableProperty] private SortHeaderOption<TEnum> _selected = null!;
    
    public event EventHandler StateChanged;
    public SortHeaderViewModel()
    {
        foreach (var option in Enum.GetValues<TEnum>())
            Options.Add(new SortHeaderOption<TEnum>(this, option));

        Options[0].IsSelected = true;
    }
    
    //Events from the View
    partial void OnSelectedChanged(SortHeaderOption<TEnum> value)
    {
        foreach (var option in Options)
            if (option.IsSelected && option != value)
                option.IsSelected = false;
        
        OnStateChanged();
    }

    public void AscendingChanged()
    {
        OnStateChanged();
    }

    protected virtual void OnStateChanged()
    {
        if (_noChangeState) return;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

public partial class SortHeaderOption<TEnum> : ObservableObject
    where TEnum : struct, Enum
{
    private SortHeaderViewModel<TEnum> _vm;
    private readonly Geometry _upGeometry = Geometry.Parse("M 4 10 L 8 6 L 12 10");
    private readonly Geometry _downGeometry = Geometry.Parse("M 4 6 L 8 10 L 12 6");
    
    [ObservableProperty] private TEnum _sortOption;
    [ObservableProperty] private bool _isAscending = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private Geometry _arrow;
    public bool ShowArrow => !IsSelected;
    public string Label => SortOption.ToString();

    public SortHeaderOption(SortHeaderViewModel<TEnum> vm, TEnum sortOption)
    {
        _vm = vm;
        _sortOption = sortOption;
        _arrow =  _upGeometry;
    }

    partial void OnIsAscendingChanged(bool value)
    {
        Arrow = IsAscending? _upGeometry : _downGeometry;
        _vm.AscendingChanged();
    }

    partial void OnIsSelectedChanging(bool oldValue, bool newValue)
    {
        if(newValue && _vm.Selected != this)
            _vm.Selected = this;
    }

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(ShowArrow));

    [RelayCommand]
    private void SetSelected()
    {
        if(!IsSelected) IsSelected = true;
        else ToggleAscending();
    }

    [RelayCommand]
    private void ToggleAscending()
    {
        IsAscending = !IsAscending;
    }
    

}