using Secs4Net;
using Secs4Net.Sml;
using mcpx_template_project.Core.Logging;

namespace mcpx_template_project.Service.Business.Secs.impl
{
    /// <summary>
    /// SECS/GEM 日志桥接：把 secs4net 的日志转到 SECS 专用 logger（SecsLog，独立 SECS_yyyyMMdd 目录）。
    /// 报文头格式：[SECS][方向][类型][消息名][0x消息ID]，正文为 SML（首行 SxFy W）。
    /// 本端为 Equipment（E），对端为 Host（H）：
    ///   MessageOut（发出）→ H&lt;-E；MessageIn（收到）→ H-&gt;E。
    ///   W 位（ReplyExpected）→ Primary，否则 Secondary；发=Sent，收=Received。
    /// </summary>
    public sealed class SecsGemLogger : ISecsGemLogger
    {
        public void MessageIn(SecsMessage msg, int id)
            => Write(msg, id, "H->E", msg.ReplyExpected ? "PrimaryReceived" : "SecondaryReceived");

        public void MessageOut(SecsMessage msg, int id)
            => Write(msg, id, "H<-E", msg.ReplyExpected ? "PrimarySent" : "SecondarySent");

        public void Debug(string msg) => SecsLog.Logger.Debug("[SECS] {Msg}", msg);

        public void Info(string msg) => SecsLog.Logger.Information("[SECS] {Msg}", msg);

        public void Warning(string msg) => SecsLog.Logger.Warning("[SECS] {Msg}", msg);

        public void Error(string msg) => SecsLog.Logger.Error("[SECS] {Msg}", msg);

        public void Error(string msg, Exception ex) => SecsLog.Logger.Error(ex, "[SECS] {Msg}", msg);

        public void Error(string msg, SecsMessage? message, Exception? ex)
            => SecsLog.Logger.Error(ex, "[SECS] {Msg}{NewLine}{Sml}", msg, Environment.NewLine, message?.ToSml() ?? "(null)");

        private static void Write(SecsMessage msg, int id, string direction, string type)
        {
            var name = string.IsNullOrEmpty(msg.Name)
                ? SecsMessageNames.Get(msg.S, msg.F) ?? $"S{msg.S}F{msg.F}"
                : msg.Name;
            // 直接拼字符串走 Raw，避免 Serilog 对字符串参数加双引号
            SecsLog.Raw($"[SECS][{direction}][{type}][{name}][0x{id:X8}]{Environment.NewLine}{ToBody(msg)}");
        }

        /// <summary>
        /// 把 SML 首行（"Name:'SxFy'W"）替换为 "SxFy W"，正文保持原缩进。
        /// </summary>
        private static string ToBody(SecsMessage msg)
        {
            var sml = msg.ToSml();
            var idx = sml.IndexOf('\n');
            var header = $"S{msg.S}F{msg.F}{(msg.ReplyExpected ? " W" : string.Empty)}";
            return idx < 0 ? header : header + sml.Substring(idx);
        }
    }
}
