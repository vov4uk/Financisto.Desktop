using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;

namespace Financisto.Reports
{
    public abstract class BaseReportVM<T> : BaseViewModel<T>, IReportVM
        where T : BaseModel, new()
    {
        private ProjectModel _project;
        private CategoryModel _category;
        private CategoryModel _topCategory;
        private AccountFilterModel _account;
        private PayeeModel _payee;
        private CurrencyModel _curentCurrency;
        private YearMonths _startYearMonths;
        private YearMonths _endYearMonths;
        private DateTime? _date;
        private DateTime? _from;
        private DateTime? _to;

        private string _header;
        private bool _isSelected;
        private ReportChart _chart = ReportChart.Empty;

        public IDialogService DialogService { get; set; } = NullDialogService.Instance;

        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>The report's main chart. Reports with a second one (a pie next to a bar chart) expose it separately.</summary>
        public ReportChart Chart
        {
            get => _chart;
            protected set => SetProperty(ref _chart, value);
        }

        protected BaseReportVM(IFinancistoDatabase financistoDatabase)
            : base(financistoDatabase)
        {
        }

        public ProjectModel Project
        {
            get => _project ??= DbManual.Project.FirstOrDefault(p => !p.Id.HasValue);
            set
            {
                _project = value;
                RaisePropertyChanged(nameof(Project));
            }
        }

        public CategoryModel Category
        {
            get => _category ??= DbManual.Category.FirstOrDefault(p => !p.Id.HasValue);
            set
            {
                _category = value;
                RaisePropertyChanged(nameof(Category));
            }
        }

        public CategoryModel TopCategory
        {
            get => _topCategory ??= DbManual.TopCategories.FirstOrDefault(p => !p.Id.HasValue);
            set
            {
                _topCategory = value;
                RaisePropertyChanged(nameof(TopCategory));
            }
        }

        public AccountFilterModel Account
        {
            get => _account ??= DbManual.Account.FirstOrDefault(p => !p.Id.HasValue);
            set
            {
                _account = value;
                RaisePropertyChanged(nameof(Account));
            }
        }

        public PayeeModel Payee
        {
            get => _payee ??= DbManual.Payee.FirstOrDefault(p => !p.Id.HasValue);
            set
            {
                _payee = value;
                RaisePropertyChanged(nameof(Payee));
            }
        }

        public CurrencyModel CurentCurrency
        {
            get => _curentCurrency ??= DbManual.Currencies.FirstOrDefault(p => !p.Id.HasValue);
            set
            {
                _curentCurrency = value;
                RaisePropertyChanged(nameof(CurentCurrency));
            }
        }

        public YearMonths StartYearMonths
        {
            get => _startYearMonths ??= DbManual.YearMonths.FirstOrDefault(p => !p.Year.HasValue && !p.Month.HasValue);
            set
            {
                _startYearMonths = value;
                RaisePropertyChanged(nameof(StartYearMonths));
            }
        }

        public YearMonths EndYearMonths
        {
            get => _endYearMonths ??= DbManual.YearMonths.FirstOrDefault(p => !p.Year.HasValue && !p.Month.HasValue);
            set
            {
                _endYearMonths = value;
                RaisePropertyChanged(nameof(EndYearMonths));
            }
        }

        public DateTime? DateFilter
        {
            get => _date;
            set
            {
                if (SetProperty(ref _date, value))
                {
                    RaisePropertyChanged(nameof(DateFilter));
                }
            }
        }

        public DateTime? From
        {
            get => _from;
            set
            {
                if (SetProperty(ref _from, value))
                {
                    RaisePropertyChanged(nameof(From));
                }
            }
        }

        public DateTime? To
        {
            get => _to;
            set
            {
                if (SetProperty(ref _to, value))
                {
                    RaisePropertyChanged(nameof(To));
                }
            }
        }

        protected override async Task RefreshData()
        {
            string sql = GetSql();
            if (!string.IsNullOrEmpty(sql))
            {
                var data = await base.db.ExecuteQuery<T>(sql);
                Entities = new ObservableCollection<T>(data);
                Chart = GetChart(data);
            }
        }

        protected abstract string GetSql();

        protected abstract ReportChart GetChart(List<T> list);

        protected string GetStandartTrnFilter()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (StartYearMonths.Year.HasValue)
                stringBuilder.Append(string.Format(" ((date_year = {0} and date_month >= {1}) or date_year > {0})", StartYearMonths.Year, StartYearMonths.Month));
            if (DateFilter.HasValue)
                stringBuilder.Append($" {new DateTimeOffset(DateFilter.Value).ToUnixTimeMilliseconds()}");
            if (EndYearMonths.Year.HasValue)
                stringBuilder.Append(string.Format(" {2} ((date_year = {0} and date_month <= {1}) or date_year < {0})", EndYearMonths.Year, EndYearMonths.Month, stringBuilder.Length != 0 ? " and " : string.Empty));
            if (Payee.Id.HasValue)
                stringBuilder.Append(string.Format(" {1} ( payee_id = {0} )", Payee.Id, stringBuilder.Length != 0 ? " and " : string.Empty));
            if (Category.Id.HasValue)
                stringBuilder.Append(string.Format(@"
{1} category_id IN
(
       SELECT _id
       FROM   category ctx,
              (
                     SELECT xxx.LEFT,
                            xxx.RIGHT
                     FROM   category xxx
                     WHERE  xxx._id = {0}) root
       WHERE  ctx.LEFT >= root.LEFT
       AND    ctx.RIGHT <= root.RIGHT
)", Category.Id, stringBuilder.Length != 0 ? " and " : string.Empty));
            if (Project.Id.HasValue)
                stringBuilder.Append(string.Format(" {1} ( project_id = {0} )", Project.Id, stringBuilder.Length != 0 ? " and " : string.Empty));
            if (Account.Id.HasValue)
                stringBuilder.Append(string.Format(" {1} ( from_account_id = {0} )", Account.Id, stringBuilder.Length != 0 ? " and " : string.Empty));
            return stringBuilder.ToString();
        }
    }
}
