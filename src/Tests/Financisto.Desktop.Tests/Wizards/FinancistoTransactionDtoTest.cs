namespace Financisto.Desktop.Tests.Wizards
{
    using System.Collections.Generic;
    using Financisto.Desktop.Wizards;
    using Xunit;

    public class FinancistoTransactionDtoTest
    {
        [Theory]
        [InlineData(0, true)]
        [InlineData(-1, true)]
        [InlineData(1, false)]
        [InlineData(42, false)]
        public void HasNoCategory_CategoryId_OnlyARealCategoryCounts(int categoryId, bool expected)
        {
            var dto = new FinancistoTransactionDto { CategoryId = categoryId };

            Assert.Equal(expected, dto.HasNoCategory);
        }

        [Fact]
        public void HasNoCategory_NewRow_True()
        {
            Assert.True(new FinancistoTransactionDto().HasNoCategory);
        }

        [Fact]
        public void CategoryId_Changed_HasNoCategoryChangedToo()
        {
            var dto = new FinancistoTransactionDto();
            var changed = new List<string>();
            dto.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            dto.CategoryId = 7;
            dto.CategoryId = 0;

            Assert.Equal(new[] { nameof(dto.CategoryId), nameof(dto.HasNoCategory), nameof(dto.CategoryId), nameof(dto.HasNoCategory) }, changed);
            Assert.True(dto.HasNoCategory);
        }
    }
}
