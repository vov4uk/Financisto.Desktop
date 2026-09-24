namespace Financisto.DataAccess.Data
{
    /// <summary>Entity with an Android "aliases" column (payee, location).</summary>
    public interface IHasAliases
    {
        /// <summary>Aliases in the backup's escaped form: joined by the two characters \n.</summary>
        string Aliases { get; set; }
    }
}
