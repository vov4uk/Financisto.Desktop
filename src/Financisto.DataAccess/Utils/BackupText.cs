using System;
using System.Text;

namespace Financisto.DataAccess.Utils
{
    /// <summary>
    /// Android's escaping of the aliases/tags columns in .backup files (DatabaseExport.escape / DatabaseImport.unescape):
    /// a line break is written as \n and a backslash as \\.
    /// </summary>
    public static class BackupText
    {
        public static string Escape(string value)
        {
            if (value == null)
                return null;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\n': sb.Append("\\n"); break;
                    case '\\': sb.Append("\\\\"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static string Unescape(string value)
        {
            if (value == null)
                return null;

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i < value.Length - 1)
                {
                    char d = value[++i];
                    sb.Append(d == 'n' ? '\n' : d);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>Aliases from the escaped column value; empty lines are skipped, as Android does when matching.</summary>
        public static string[] SplitAliases(string escaped) =>
            Unescape(escaped)?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    }
}
