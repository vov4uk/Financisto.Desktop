using System;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Prism.Mvvm;

namespace Financisto.Desktop.Data
{
    public class RuleDto : BindableBase
    {
        private CategoryModel category;
        private int? categoryId;
        private RuleConditionType condition;
        private string description;
        private bool isActive;
        private int? locationId;
        private int? payeeId;
        private int? projectId;
        private Mcc mccCategory;
        public RuleDto()
        {
        }

        public RuleDto(RuleModel rulesModel)
        {
            Description = rulesModel.Description;
            Condition = rulesModel.Condition;
            IsActive = rulesModel.IsActive;
            PayeeId = rulesModel.PayeeId;
            ProjectId = rulesModel.ProjectId;
            CategoryId = rulesModel.CategoryId;
            LocationId = rulesModel.LocationId;
            Created = rulesModel.Created;
            MCCCategory = rulesModel.MCCCategory;
        }

        public CategoryModel Category
        {
            get => category ??= DbManual.Category?.Find(x => x.Id == CategoryId);
            set => SetProperty(ref category, value);
        }

        public int? CategoryId
        {
            get => categoryId;
            set => SetProperty(ref categoryId, value);
        }

        public RuleConditionType Condition
        {
            get => condition;
            set
            {
                condition = value;
                RaisePropertyChanged(nameof(Condition));
            }
        }

        public DateTime Created { get; set; }

        public string Description
        {
            get => description;
            set
            {
                description = value;
                RaisePropertyChanged(nameof(Description));
            }
        }
        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                RaisePropertyChanged(nameof(IsActive));
            }
        }
        public int? LocationId
        {
            get => locationId;
            set => SetProperty(ref locationId, value);
        }

        public int? PayeeId
        {
            get => payeeId;
            set => SetProperty(ref payeeId, value);
        }

        public int? ProjectId
        {
            get => projectId;
            set => SetProperty(ref projectId, value);
        }

        public Mcc MCCCategory
        {
            get => mccCategory;
            set => SetProperty(ref mccCategory, value);
        }
    }
}
