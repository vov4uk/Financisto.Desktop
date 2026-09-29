using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.Converters;
using Financisto.Desktop.Data;

namespace Financisto.Desktop.ViewModels.Dialogs;

public class RuleDialogVM : DialogBaseVM
{
    private readonly string _noneMccTitle;
    private RuleConditionType _selectedConditionType;
    private string _selectedMccTitle;

    public RuleDialogVM(RuleDto entity)
    {
        Entity = entity;
        SelectedConditionType = entity.Condition;
        Entity.PropertyChanged += Entity_PropertyChanged;

        MccTitles = DbManual.MCCTitles.Keys.OrderBy(x => x).ToList();
        SelectedMccTitle = entity.MCCCategory.GetEnumLocalizedMccDescription();
        _noneMccTitle = Mcc.none.GetEnumLocalizedMccDescription();
    }

    public RuleDto Entity { get; }

    public List<string> MccTitles { get; }

    public RuleConditionType SelectedConditionType
    {
        get => _selectedConditionType;
        set
        {
            if (SetProperty(ref _selectedConditionType, value))
            {
                OnPropertyChanged(nameof(IsMCCSelected));
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedMccTitle
    {
        get => _selectedMccTitle;
        set
        {
            if (SetProperty(ref _selectedMccTitle, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsMCCSelected => SelectedConditionType == RuleConditionType.MCC;

    public override object OnRequestSave()
    {
        Entity.Condition = SelectedConditionType;
        if (!IsMCCSelected)
        {
            Entity.MCCCategory = Mcc.none;
        }
        else
        {
            Entity.Description = null;
            Entity.MCCCategory = DbManual.MCCTitles[SelectedMccTitle];
        }
        return Entity;
    }

    protected override bool CanSaveCommandExecute()
    {
        bool conditionMeets = (IsMCCSelected && !string.IsNullOrEmpty(SelectedMccTitle) && SelectedMccTitle != _noneMccTitle && DbManual.MCCTitles.ContainsKey(SelectedMccTitle))
            || (!IsMCCSelected && !string.IsNullOrEmpty(Entity.Description));
        bool actionMeets = Entity.PayeeId != null || Entity.LocationId != null || Entity.CategoryId != null || Entity.ProjectId != null;
        return conditionMeets && actionMeets;
    }

    private void Entity_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }
}
