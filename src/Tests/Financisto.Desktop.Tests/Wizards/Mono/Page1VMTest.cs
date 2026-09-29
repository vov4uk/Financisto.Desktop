namespace Financisto.Desktop.Tests.Wizards.Mono
{
    using System;
    using System.Collections.Generic;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.Desktop.Wizards.MonoWizard.ViewModel;
    using Financisto.Tests.Common;
    using Xunit;

    public class Page1VMTest
    {
        [Theory]
        [AutoMoqData]
        public void Constructor_HasMonoAccount_SelectedMonoAccount(List<AccountFilterModel> accounts)
        {
            var monoAcc = new AccountFilterModel { Title = "Monobank", IsActive = true };
            accounts.Add(monoAcc);
            DbManual.SetupTests(accounts);
            var vm = new Page1VM("Monobank");

            Assert.Equal(monoAcc, vm.MonoAccount);
            Assert.Equal("Please select account", vm.Title);
            Assert.True(vm.IsValid());
            DbManual.ResetAllDatabaseManuals();
        }

        [Theory]
        [AutoMoqData]
        public void Constructor_MonoAccountInactive_MonoAccountNotSelected(List<AccountFilterModel> accounts)
        {
            var monoAcc = new AccountFilterModel { Title = "Monobank", IsActive = false };
            accounts.Add(monoAcc);
            DbManual.SetupTests(accounts);
            var vm = new Page1VM("Monobank");

            Assert.Equal(accounts[0], vm.MonoAccount);
            DbManual.ResetAllDatabaseManuals();
        }

        [Theory]
        [AutoMoqData]
        public void Constructor_NoMono_FirstAccountSelected(List<AccountFilterModel> accounts)
        {
            foreach (var item in accounts)
            {
                item.Title = Guid.NewGuid().ToString();
            }

            DbManual.SetupTests(accounts);
            var vm = new Page1VM("Monobank");

            Assert.NotNull(vm.MonoAccount);
            DbManual.ResetAllDatabaseManuals();
        }
    }
}
