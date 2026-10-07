using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Media;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Desktop.Data;

namespace Financisto.Desktop.ViewModels.Dialogs;

/// <summary>The create/edit dialog of an Android-compatible SMS template.</summary>
public class SmsTemplateDialogVM : DialogBaseVM
{
    private static readonly string[] PlaceholderNames =
    [
        "ANY", "ACCOUNT", "BALANCE", "ACCOUNT_NAME", "DATE", "PAYEE", "CURRENCY", "TIMESTAMP_MILLIS", "PRICE", "PROJECT",
        "TEXT", "GREEDY_TEXT", "TRANSFER_TO_ACCOUNT_NAME",
    ];

    private static readonly IBrush MatchBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x4C, 0xAF, 0x50));
    private static readonly IBrush NoMatchBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xF4, 0x43, 0x36));

    private string _example = string.Empty;
    private string _parseResult = string.Empty;
    private IBrush _exampleBackground = Brushes.Transparent;

    public SmsTemplateDialogVM(SmsTemplateDto entity)
    {
        Entity = entity;
        Entity.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SmsTemplateDto.Template))
            {
                UpdateParseResult();
            }

            SaveCommand.NotifyCanExecuteChanged();
        };

        Accounts = BuildAccounts(LocalizationService.Instance.no_account);
        ToAccounts = BuildAccounts(LocalizationService.Instance.tpl_not_transfer);
    }

    public SmsTemplateDto Entity { get; }

    public List<AccountFilterModel> Accounts { get; }

    public List<AccountFilterModel> ToAccounts { get; }

    public bool IsExpense
    {
        get => !Entity.IsIncome;
        set
        {
            if (value)
            {
                Entity.IsIncome = false;
                NotifyType();
            }
        }
    }

    public bool IsIncome
    {
        get => Entity.IsIncome;
        set
        {
            if (value)
            {
                Entity.IsIncome = true;
                NotifyType();
            }
        }
    }

    public bool MatchNormal
    {
        get => !Entity.MatchGroupSummary;
        set
        {
            if (value)
            {
                Entity.MatchGroupSummary = false;
                NotifyMatch();
            }
        }
    }

    public bool MatchGroupSummary
    {
        get => Entity.MatchGroupSummary;
        set
        {
            if (value)
            {
                Entity.MatchGroupSummary = true;
                NotifyMatch();
            }
        }
    }

    /// <summary>An example notification the user pastes in to check what the template extracts.</summary>
    public string Example
    {
        get => _example;
        set
        {
            if (SetProperty(ref _example, value ?? string.Empty))
            {
                UpdateParseResult();
            }
        }
    }

    public string ParseResult
    {
        get => _parseResult;
        private set => SetProperty(ref _parseResult, value);
    }

    public IBrush ExampleBackground
    {
        get => _exampleBackground;
        private set => SetProperty(ref _exampleBackground, value);
    }

    public override object OnRequestSave() => Entity;

    protected override bool CanSaveCommandExecute()
        => !string.IsNullOrWhiteSpace(Entity.Sender) && !string.IsNullOrWhiteSpace(Entity.Template);

    private static List<AccountFilterModel> BuildAccounts(string emptyTitle)
    {
        var list = new List<AccountFilterModel> { new() { Title = emptyTitle, IsActive = true } };
        list.AddRange(DbManual.SelectableAccounts);
        return list;
    }

    private void NotifyType()
    {
        OnPropertyChanged(nameof(IsExpense));
        OnPropertyChanged(nameof(IsIncome));
    }

    private void NotifyMatch()
    {
        OnPropertyChanged(nameof(MatchNormal));
        OnPropertyChanged(nameof(MatchGroupSummary));
    }

    /// <summary>Mirrors the Android dialog: nothing changes until both fields have content and the template is longer than 4 characters.</summary>
    private void UpdateParseResult()
    {
        string template = Entity.Template;
        if (string.IsNullOrEmpty(template) || template.Length <= 4 || string.IsNullOrEmpty(_example))
        {
            return;
        }

        string[]? matches = SmsTemplateParser.FindTemplateMatches(template, _example);
        if (matches == null)
        {
            ExampleBackground = NoMatchBrush;
            ParseResult = string.Empty;
            return;
        }

        ExampleBackground = MatchBrush;
        var text = new StringBuilder();
        for (int i = 0; i < SmsTemplateParser.PlaceholderCount; i++)
        {
            text.Append(PlaceholderNames[i]).Append(": ");
            if (matches[i] == null)
            {
                text.Append(LocalizationService.Instance.tpl_parse_not_found);
            }
            else if (i == (int)SmsPlaceholder.Price)
            {
                try
                {
                    text.Append(SmsTemplateParser.ToDecimal(matches[i]).ToString(CultureInfo.InvariantCulture));
                }
                catch (FormatException)
                {
                    text.Append(string.Format(LocalizationService.Instance.tpl_failed_to_parse, matches[i]));
                }
            }
            else
            {
                text.Append(matches[i]);
            }

            text.AppendLine();
        }

        ParseResult = text.ToString();
    }
}
