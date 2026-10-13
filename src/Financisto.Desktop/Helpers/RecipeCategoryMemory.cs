using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Financisto.Common.Entities;
using Financisto.Desktop.Wizards;

namespace Financisto.Desktop.Helpers;

/// <summary>
/// What the user chose for a receipt line the last times: note -> category title, kept in a plain JSON text file next to the settings.
/// The recipes wizard reads it when it starts and fills the category of every line whose note it has seen (exact note, case ignored),
/// and adds the lines of a receipt when the wizard is saved (and before Clear notes wipes the notes).
/// The title, not the id, is stored: the file belongs to the app and not to one backup, and it stays readable and editable by hand.
/// </summary>
public sealed class RecipeCategoryMemory
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Polish/Ukrainian letters readable in the file
    };

    private readonly string path;
    private readonly Dictionary<string, string> categories = new(StringComparer.OrdinalIgnoreCase);

    public RecipeCategoryMemory(string path)
    {
        this.path = path;
    }

    /// <summary>The file next to <c>Settings.dat</c>.</summary>
    public static string DefaultPath =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(StartOptions.Current.SettingsPath))!, "RecipeCategories.json");

    /// <summary>Note -> category title, as remembered now (the file after <see cref="Load"/> plus what was added since).</summary>
    public IReadOnlyDictionary<string, string> Categories => categories;

    /// <summary>Reads the file; a missing or damaged file just means nothing is remembered (the wizard must open anyway).</summary>
    public void Load()
    {
        categories.Clear();
        if (!File.Exists(path))
        {
            Logger.Info($"[Recipes] no remembered categories yet ({path})");
            return;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), JsonOptions);
            foreach (var (note, category) in stored ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrWhiteSpace(note) && !string.IsNullOrWhiteSpace(category))
                {
                    categories[note.Trim()] = category.Trim();
                }
            }

            Logger.Info($"[Recipes] {categories.Count} remembered categories read from {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.Warn(ex, $"The remembered receipt categories {path} could not be read, starting with none");
            categories.Clear();
        }
    }

    /// <summary>
    /// Remembers the category of every row that has a note and a category; the others are ignored (a row without a note has nothing to find
    /// it by, a row without a category would only forget what is known). Only in memory until <see cref="Save"/>.
    /// </summary>
    public void Remember(IEnumerable<FinancistoTransactionDto> rows)
    {
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Note) || row.CategoryId <= 0)
            {
                continue;
            }

            var title = DbManual.Category?.FirstOrDefault(category => category.Id == row.CategoryId)?.Title;
            if (!string.IsNullOrWhiteSpace(title))
            {
                categories[row.Note.Trim()] = title.Trim();
            }
        }
    }

    /// <summary>The category remembered for the note, when its title still names a category.</summary>
    public bool TryGetCategoryId(string? note, out int categoryId)
    {
        categoryId = 0;
        if (string.IsNullOrWhiteSpace(note) || !categories.TryGetValue(note.Trim(), out var title))
        {
            return false;
        }

        // Titles can repeat under different parents; the first one is taken
        var category = DbManual.Category?.FirstOrDefault(c => c.Id > 0 && string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));
        categoryId = category?.Id ?? 0;
        return categoryId > 0;
    }

    /// <summary>Writes the file; a failure is logged and does not stop saving the receipt.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(categories.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(pair => pair.Key, pair => pair.Value), JsonOptions));
            File.Move(temp, path, overwrite: true);
            Logger.Info($"[Recipes] {categories.Count} remembered categories saved to {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Error(ex, $"The remembered receipt categories could not be saved to {path}");
        }
    }
}
