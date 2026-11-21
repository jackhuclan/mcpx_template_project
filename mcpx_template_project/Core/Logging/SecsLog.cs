using System;
using System.IO;
using System.Text;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace mcpx_template_project.Core.Logging
{
    /// <summary>
    /// SECS 专用日志：独立于系统日志，按天分文件夹输出（SECS_yyyyMMdd/secs.log）。
    /// 系统日志走全局 Serilog（logs/），SECS 报文与运行状态日志统一走本 logger。
    /// </summary>
    public static class SecsLog
    {
        private static readonly Lazy<SecsDailyFolderSink> Sink =
            new(() => new SecsDailyFolderSink(AppContext.BaseDirectory));

        private static readonly Lazy<ILogger> LazyLogger = new(() =>
            new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(Sink.Value)
                .CreateLogger());

        /// <summary>SECS 独立 logger（写入 SECS_yyyyMMdd 目录）</summary>
        public static ILogger Logger => LazyLogger.Value;

        /// <summary>
        /// 直接写一行裸文本（不含时间戳），时间戳由 sink 添加。
        /// 用于 SECS 报文日志——绕过 Serilog 消息模板，避免字符串参数被自动加双引号。
        /// </summary>
        public static void Raw(string message) => Sink.Value.WriteRaw(message);

        /// <summary>
        /// 按天目录滚动的轻量 sink：每跨一天，在根目录下新建 SECS_yyyyMMdd 文件夹并追加写入 secs.log。
        /// 输出格式：{时间 HH:mm:ss.fff zzz} {消息}。
        /// </summary>
        private sealed class SecsDailyFolderSink : ILogEventSink, IDisposable
        {
            private readonly object _sync = new();
            private readonly string _root;
            private string? _date;
            private StreamWriter? _writer;

            public SecsDailyFolderSink(string root) => _root = root;

            public void Emit(LogEvent logEvent) => WriteLine(logEvent.RenderMessage());

            public void WriteRaw(string message) => WriteLine(message);

            private void WriteLine(string message)
            {
                var now = DateTimeOffset.Now;
                var date = now.ToString("yyyyMMdd");
                lock (_sync)
                {
                    if (_date != date)
                    {
                        _writer?.Dispose();
                        var dir = Path.Combine(_root, $"SECS_{date}");
                        Directory.CreateDirectory(dir);
                        _writer = new StreamWriter(
                            Path.Combine(dir, "secs.log"), append: true, Encoding.UTF8);
                        _date = date;
                    }
                    _writer!.WriteLine($"{now:HH:mm:ss.fff zzz} {message}");
                    _writer.Flush();
                }
            }

            public void Dispose()
            {
                lock (_sync) _writer?.Dispose();
            }
        }
    }
}
