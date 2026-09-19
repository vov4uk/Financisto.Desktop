namespace Financisto.Common
{
    public interface IDataRefresh
    {
        IAsyncCommand RefreshDataCommand { get; }
    }
}
