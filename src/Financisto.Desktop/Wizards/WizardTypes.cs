using System.ComponentModel;

namespace Financisto.Desktop.Wizards
{
    /// <summary>Bank statement import sources; the description is the statement file extension.</summary>
    public enum WizardTypes
    {
        [Description("csv")]
        Monobank,
        [Description("pdf")]
        ABank,
        [Description("xlsx")]
        ABankExcel,
        [Description("xlsx")]
        Privat,
        [Description("pdf")]
        Pumb,
        [Description("pdf")]
        Pireus,
        [Description("pdf")]
        Pko,
        [Description("csv")]
        Revolut
    }
}
