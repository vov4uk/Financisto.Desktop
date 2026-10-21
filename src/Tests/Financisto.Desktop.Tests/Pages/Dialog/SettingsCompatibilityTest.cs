namespace Financisto.Desktop.Tests.Pages.Dialog
{
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;
    using Financisto.Common.Localization;
    using Financisto.Desktop.Data;
    using Xunit;

    /// <summary>Settings files written by an older version still hold the removed "IconSet" option: they must load, the value is just ignored.</summary>
    public partial class SettingsCompatibilityTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Settings_FileWithTheRemovedIconSetOption_StillLoads(int iconSet)
        {
            var current = new SettingsDto
            {
                General = new SettingsGeneralDto { Language = Language.Ukrainian, CheckForUpdatesOnStart = true, DefaultBackupDir = "C:\\backups" },
                ExchangeRates = new SettingsExchangeRates(),
            };
            var json = JsonNode.Parse(JsonSerializer.Serialize(current, TestContext.Default.SettingsDto))!;
            json["General"]!["IconSet"] = iconSet;

            var loaded = JsonSerializer.Deserialize(json.ToJsonString(), TestContext.Default.SettingsDto);

            Assert.NotNull(loaded);
            Assert.Equal(Language.Ukrainian, loaded.General.Language);
            Assert.True(loaded.General.CheckForUpdatesOnStart);
            Assert.Equal("C:\\backups", loaded.General.DefaultBackupDir);
        }

        [Fact]
        public void Settings_WhatIsSaved_HasNoIconSetOption()
        {
            var json = JsonSerializer.Serialize(new SettingsDto { General = new SettingsGeneralDto(), ExchangeRates = new SettingsExchangeRates() }, TestContext.Default.SettingsDto);

            Assert.DoesNotContain("IconSet", json);
        }

        [JsonSerializable(typeof(SettingsDto))]
        internal sealed partial class TestContext : JsonSerializerContext;
    }
}
