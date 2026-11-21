namespace mcpx_template_project.Core.Handler
{
    /// <summary>
    /// PLC 事件宿主标记接口：实现本接口的类中，带 <c>[PlcEvent]</c> 注解的方法
    /// 会被 <see cref="DependencyInjection.PlcEventHandlerRegistry"/> 扫描登记，
    /// 供 PLC 触发器配点界面下拉框选择，并在点位值变化时反射调用。
    /// 本接口不要求实现任何成员，仅用于限定扫描范围与 DI 注册。
    /// </summary>
    /// <author>Rudeus</author>
    public interface IPlcEventHost
    {
    }
}
