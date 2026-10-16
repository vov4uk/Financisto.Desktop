namespace Financisto.Desktop.Tests.Pages.Dialog
{
    using System;
    using System.IO;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using Xunit;

    /// <summary>
    /// The layout of the app's log file. Without the exception in it, <c>Logger.Error(ex, "...")</c> wrote only the message and the type, message and stack
    /// trace of every logged failure were lost (the receipt wizard, OCR and crash logging all pass the exception as an argument).
    /// </summary>
    public class NLogConfigTests
    {
        private static Layout FileLayout()
        {
            var config = new XmlLoggingConfiguration(Path.Combine(AppContext.BaseDirectory, "NLog.config"));
            return config.FindTargetByName<FileTarget>("file")!.Layout;
        }

        [Fact]
        public void Layout_LoggedException_TypeMessageAndStackTraceAreInTheFile()
        {
            Exception exception;
            try
            {
                throw new InvalidOperationException("boom detail");
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }

            var text = FileLayout().Render(new LogEventInfo(LogLevel.Error, "Test", "something failed") { Exception = exception });

            Assert.Contains("|ERROR|", text);
            Assert.Contains("|something failed", text);
            Assert.Contains("System.InvalidOperationException: boom detail", text);
            Assert.Contains(nameof(Layout_LoggedException_TypeMessageAndStackTraceAreInTheFile), text); // a frame of the stack trace
        }

        [Fact]
        public void Layout_InnerException_WrittenToo()
        {
            var exception = new InvalidOperationException("outer", new ArgumentException("the inner reason"));

            var text = FileLayout().Render(new LogEventInfo(LogLevel.Error, "Test", "failed") { Exception = exception });

            Assert.Contains("the inner reason", text);
        }

        [Fact]
        public void Layout_NoException_StaysOneLine()
        {
            var text = FileLayout().Render(new LogEventInfo(LogLevel.Info, "Test", "just a message"));

            Assert.EndsWith("|just a message", text);
            Assert.DoesNotContain(Environment.NewLine, text);
        }
    }
}
