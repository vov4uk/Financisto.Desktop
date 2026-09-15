using System;
using Financisto.Desktop.Wizards;

namespace Financisto.Desktop.Helpers.BankHelper
{
    public interface IBankHelperFactory
    {
        IBankHelper CreateBankHelper(WizardTypes bank);
    }

    public class BankHelperFactory : IBankHelperFactory
    {
        public IBankHelper CreateBankHelper(WizardTypes bank)
        {
            //switch (bank)
            //{
            //    case WizardTypes.Monobank: return new MonobankHelper();
            //    case WizardTypes.ABank: return new ABankHelper();
            //    case WizardTypes.ABankExcel: return new AbankExcelHelper();
            //    case WizardTypes.Pumb: return new PumbHelper();
            //    case WizardTypes.Pireus: return new PireusHelper();
            //    case WizardTypes.Privat: return new PrivatHelper();
            //    case WizardTypes.Pko: return new PkoHelper();
            //    case WizardTypes.Revolut: return new RevolutHelper();
            //    default:
                    throw new NotSupportedException("Bank not found");
            //}
        }
    }
}
