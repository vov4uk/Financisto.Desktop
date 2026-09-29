namespace Financisto.Reports
{
    /// <summary>Default <see cref="IDialogService"/> until the host app supplies one: the message is only logged.</summary>
    internal sealed class NullDialogService : IDialogService
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static readonly IDialogService Instance = new NullDialogService();

        private NullDialogService() { }

        public void ShowMessage(string message) => Logger.Warn(message);
    }
}
