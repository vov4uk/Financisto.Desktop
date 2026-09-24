using Financisto.DataAccess.Data;
using Financisto.Adapter.Converters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Financisto.DataAccess.Utils;

namespace Financisto.Adapter
{
    public static class EntityExtensions
    {
        private static readonly char[] LineBreaks = ['\r', '\n'];

        private record struct ColumnInfo(string Col, Func<Entity, object> GetValue, IPropertyConverter Conv, object DefaultValue, bool EscapeLineBreaks);
        private record struct TypeInfo(string TableName, ColumnInfo[] Columns, Func<Entity, object> GetSortOrder);

        private static readonly ConcurrentDictionary<Type, TypeInfo> _typeCache = new();

        public static void WriteBackupLines(
            this Entity entity,
            TextWriter writer,
            Dictionary<string, (Dictionary<string, int> Index, int Count)> allColumnData)
        {
            Type type = entity.GetType();
            TypeInfo info = _typeCache.GetOrAdd(type, BuildTypeInfo);

            if (info.TableName == string.Empty) return;

            // A table absent from the source backup (e.g. the first tag created in the app) is written with all its columns.
            bool isKnownTable = allColumnData.TryGetValue(info.TableName, out var colData);
            var (columnIndex, columnCount) = colData;
            string[] lines = new string[isKnownTable ? columnCount : 0];
            List<string> extraLines = null;

            foreach (ColumnInfo col in info.Columns)
            {
                object val = col.GetValue(entity);
                if (val == null)
                    continue;

                string line = $"{col.Col}:{ToSingleLine(col.Conv.ConvertBack(val), col.EscapeLineBreaks)}";
                if (isKnownTable && columnIndex.TryGetValue(col.Col, out int colIdx))
                {
                    lines[colIdx] = line;
                }
                else if (!isKnownTable || !Equals(val, col.DefaultValue))
                {
                    // Column absent from the source backup: write it only when the app set a non-default value,
                    // so untouched rows keep the source format.
                    (extraLines ??= new List<string>()).Add(line);
                }
            }

            writer.WriteLine($"{Backup.ENTITY}:{info.TableName}");
            foreach (string line in lines)
            {
                if (line != null)
                    writer.WriteLine(line);
            }
            if (extraLines != null)
            {
                foreach (string line in extraLines)
                    writer.WriteLine(line);
            }
            writer.WriteLine(Backup.ENTITY_END);
        }

        /// <summary>
        /// Orders rows of one entity type like Android's export: tables with a sort order are written "order by sort_order",
        /// which is how Android restores it. Rows added in the app (sort order 0) go last, as Android appends new entities.
        /// </summary>
        public static IEnumerable<Entity> InBackupOrder(this IEnumerable<Entity> rows, Type type)
        {
            Func<Entity, object> getSortOrder = _typeCache.GetOrAdd(type, BuildTypeInfo).GetSortOrder;
            if (getSortOrder == null)
                return rows;

            return rows.OrderBy(e =>
            {
                long order = Convert.ToInt64(getSortOrder(e));
                return order > 0 ? order : long.MaxValue;
            });
        }

        // Every value must stay on its line: Android escapes line breaks in aliases/tags and replaces them with a space elsewhere.
        private static string ToSingleLine(string value, bool escape)
        {
            if (value.IndexOfAny(LineBreaks) < 0)
                return value;

            string replacement = escape ? "\\n" : " ";
            return value.Replace("\r\n", replacement).Replace("\r", replacement).Replace("\n", replacement);
        }

        private static TypeInfo BuildTypeInfo(Type type)
        {
            string tableName = type.GetCustomAttributes().OfType<TableAttribute>().FirstOrDefault()?.Name;
            if (tableName == null)
                return new TypeInfo(string.Empty, Array.Empty<ColumnInfo>(), null);

            object defaultInstance = type.IsAbstract ? null : Activator.CreateInstance(type);

            ColumnInfo[] columns = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<IgnoreAttribute>() == null)
                .Select(p => (Attr: p.GetCustomAttribute<ColumnAttribute>(), Prop: p))
                .Where(x => x.Attr != null)
                .Select(x => new ColumnInfo(
                    x.Attr!.Name!,
                    BuildGetter(x.Prop),
                    new DefaultConverter { PropertyType = x.Prop.PropertyType },
                    defaultInstance == null ? null : x.Prop.GetValue(defaultInstance),
                    x.Attr.Name is Backup.AliasesColumn or Backup.TagsColumn))
                .ToArray();

            Func<Entity, object> getSortOrder = Backup.TableHasOrder(tableName)
                ? columns.FirstOrDefault(c => c.Col == Backup.SortOrderColumn).GetValue
                : null;

            // Like Android, sort_order itself is exported only for accounts; other tables carry it in the row order.
            if (tableName != Backup.ACCOUNT_TABLE)
                columns = columns.Where(c => c.Col != Backup.SortOrderColumn).ToArray();

            return new TypeInfo(tableName, columns, getSortOrder);
        }

        private static Func<Entity, object> BuildGetter(PropertyInfo prop)
        {
            var entityParam = Expression.Parameter(typeof(Entity), "e");
            var castEntity = Expression.Convert(entityParam, prop.DeclaringType!);
            var access = Expression.Property(castEntity, prop);
            var boxed = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<Entity, object>>(boxed, entityParam).Compile();
        }
    }
}
