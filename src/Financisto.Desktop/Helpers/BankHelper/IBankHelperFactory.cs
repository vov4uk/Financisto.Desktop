using Financisto.Desktop.Wizards;

namespace Financisto.Desktop.Helpers.BankHelper
{
    public interface IBankHelperFactory
    {
        IBankHelper CreateBankHelper(WizardTypes bank);
    }
}
