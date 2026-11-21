using mcpx_template_project.Core.Handler;
using mcpx_template_project.Core.Logging;
using mcpx_template_project.Domain.Model.Plc;
using mcpx_template_project.Service.Business.Trigger;

namespace mcpx_template_project.Service.Business.Trigger.impl
{
    /// <summary>
    /// 示例事件宿主：演示方法级 [PlcEvent] 注解的用法。
    /// 只需实现 <see cref="IPlcEventHost"/> 标记接口，然后在任意方法上打 [PlcEvent] 注解，
    /// 即可被 PLC 触发器配点界面下拉框选中，并在点位值变化时被反射调用。
    ///
    /// 方法签名约定：Task Xxx(PlcEventContext ctx) 或 Task Xxx()，返回类型必须为 Task。
    /// 一个宿主类可承载多个动作，按需增减方法即可，无需为每个动作新建类。
    /// </summary>
    /// <author>Rudeus</author>
    public class LogPlcEventHandler : IPlcEventHost
    {
        private readonly ILogService _logService;

        public LogPlcEventHandler(ILogService logService)
        {
            _logService = logService;
        }

        /// <summary>写操作日志：点位值变化时记录一条日志。</summary>
        [PlcEvent("log_event", "写入操作日志", Description = "触发时记录一条日志")]
        public Task LogEvent(PlcEventContext ctx)
        {
            _logService.LogInformation(
                "[PLC事件] {Name} 触发：点位 {Address}，当前值 {Value}，时间 {Time}",
                ctx.Name, ctx.FullAddress, ctx.CurrentValue, ctx.TriggerTime);

            return Task.CompletedTask;
        }

        /// <summary>示例二：无参方法同样可被选中（不接收上下文）。</summary>
        [PlcEvent("log_event_simple", "写入日志(无参)", Description = "无参方法示例")]
        public Task LogEventSimple()
        {
            _logService.LogInformation("[PLC事件] 无参事件处理器被触发");
            return Task.CompletedTask;
        }
    }
}
