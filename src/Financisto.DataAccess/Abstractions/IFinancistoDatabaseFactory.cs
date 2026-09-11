namespace Financisto.DataAccess.Abstractions
{
    public interface IFinancistoDatabaseFactory
    {
        IFinancistoDatabase CreateDatabase();
    }
}
