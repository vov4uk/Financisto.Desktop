namespace Financisto.DataAccess.Abstractions
{
    public interface IUnitOfWorkFactory
    {
        public IUnitOfWork CreateUnitOfWork();
    }
}
