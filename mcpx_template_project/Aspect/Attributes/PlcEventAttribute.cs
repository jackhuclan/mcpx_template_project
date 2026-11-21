using mcpx_template_project.Core.Handler;

namespace mcpx_template_project.Service.Business.Trigger
{
    /// <summary>
    /// PLC 事件处理器标记（方法级）：打在实现 <see cref="IPlcEventHost"/> 的类的任意方法上，
    /// 声明「该方法可被 PLC 触发器选中，并在点位值变化时被反射调用」。
    /// </summary>
    /// <remarks>
    /// 方法签名约定：
    ///   Task Xxx(PlcEventContext ctx)  —— 可选首参数，接收触发上下文；
    ///   Task Xxx()                     —— 无参数亦可；
    ///   返回类型必须为 Task（同步逻辑返回 Task.CompletedTask）。
    /// </remarks>
    /// <author>Rudeus</author>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class PlcEventAttribute : Attribute
    {
        /// <summary>处理器唯一 Key（表中 action_key 存此值）</summary>
        public string Key { get; }

        /// <summary>处理器显示名（下拉框展示）</summary>
        public string DisplayName { get; }

        /// <summary>处理器描述（可选）</summary>
        public string? Description { get; set; }

        public PlcEventAttribute(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }
    }
}
