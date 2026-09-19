using Financisto.DataAccess.Abstractions;

namespace Financisto.DataAccess
{
    public class FinancistoDatabaseFactory : IFinancistoDatabaseFactory
    {
        public IFinancistoDatabase CreateDatabase() => new FinancistoDatabase();
    }
}
