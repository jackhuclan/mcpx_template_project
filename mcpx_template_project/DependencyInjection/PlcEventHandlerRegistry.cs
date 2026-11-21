using System.Collections.Concurrent;
using System.Reflection;
using mcpx_template_project.Core.Handler;
using mcpx_template_project.Core.Logging;
using mcpx_template_project.Domain.Model.Plc;
using mcpx_template_project.Domain.VO.Trigger;
using mcpx_template_project.Service.Business.Trigger;
using Microsoft.Extensions.DependencyInjection;

namespace mcpx_template_project.DependencyInjection
{
    /// <summary>
    /// 单个 PLC 事件处理方法的运行时描述（供配点界面展示 + 反射调用）。
    /// </summary>
    public sealed class PlcEventDescriptor
    {
        /// <summary>处理器 Key</summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>承载方法的宿主实例</summary>
        public IPlcEventHost Host { get; init; } = null!;

        /// <summary>承载方法的类型</summary>
        public Type DeclaringType { get; init; } = null!;

        /// <summary>方法名</summary>
        public string MethodName { get; init; } = string.Empty;

        /// <summary>方法元数据（用于反射调用）</summary>
        public MethodInfo Method { get; init; } = null!;

        /// <summary>是否有 PlcEventContext 首参数</summary>
        public bool HasContextParam { get; init; }
    }

    /// <summary>
    /// PLC 事件处理器注册表（方法级）。
    /// 维护「处理器 Key → 方法描述」的映射，元数据包含显示名、描述与承载方法。
    /// 提供：
    ///  1. <see cref="RegisterAll"/>：扫描装配中所有 <see cref="IPlcEventHost"/> 实现里带 <c>[PlcEvent]</c> 注解的方法并登记；
    ///  2. <see cref="GetOptions"/>：返回所有处理器选项（供配点界面下拉框展示）；
    ///  3. <see cref="InvokeAsync"/>：根据 Key 反射调用对应方法（可注入 <see cref="PlcEventContext"/> 首参数）。
    /// </summary>
    public class PlcEventHandlerRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logger;
        private readonly ConcurrentDictionary<string, PlcEventHandlerOption> _handlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, PlcEventDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

        public PlcEventHandlerRegistry(IServiceProvider serviceProvider, ILogService logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            RegisterAll();
        }

        /// <summary>扫描装配中所有 IPlcEventHost 实现里带 [PlcEvent] 注解的方法，按 Key 登记。</summary>
        private void RegisterAll()
        {
            var hostType = typeof(IPlcEventHost);
            var hosts = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException) { return Type.EmptyTypes; }
                })
                .Where(t => hostType.IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

            int count = 0;
            foreach (var host in hosts)
            {
                var instance = _serviceProvider.GetService(host) as IPlcEventHost
                               ?? ActivatorUtilities.CreateInstance(_serviceProvider, host) as IPlcEventHost;
                if (instance is null) continue;

                foreach (var method in host.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attr = method.GetCustomAttribute<PlcEventAttribute>();
                    if (attr is null) continue;

                    if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                    {
                        _logger.LogWarning("[PLC事件] 跳过 {Type}.{Method}：返回类型应为 Task",
                            host.Name, method.Name);
                        continue;
                    }

                    var ps = method.GetParameters();
                    bool hasContext = ps.Length > 0 && ps[0].ParameterType == typeof(PlcEventContext);

                    if (_descriptors.ContainsKey(attr.Key))
                    {
                        _logger.LogWarning("[PLC事件] Key {Key} 重复定义（{Type}.{Method}），后者覆盖前者",
                            attr.Key, host.Name, method.Name);
                    }

                    var descriptor = new PlcEventDescriptor
                    {
                        Key = attr.Key,
                        Host = instance,
                        DeclaringType = host,
                        MethodName = method.Name,
                        Method = method,
                        HasContextParam = hasContext,
                    };

                    _descriptors[attr.Key] = descriptor;
                    _handlers[attr.Key] = new PlcEventHandlerOption
                    {
                        Key = attr.Key,
                        DisplayName = attr.DisplayName,
                        Description = attr.Description,
                    };
                    count++;
                }
            }

            _logger.LogInformation("[PLC事件] 事件处理器注册完成，共 {Count} 个方法", count);
        }

        /// <summary>返回所有已注册处理器选项（按显示名排序），供下拉框绑定。</summary>
        public IReadOnlyList<PlcEventHandlerOption> GetOptions()
        {
            return _handlers.Values.OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>判断指定 Key 是否已注册。</summary>
        public bool Contains(string? key)
        {
            return !string.IsNullOrWhiteSpace(key) && _handlers.ContainsKey(key);
        }

        /// <summary>根据 Key 反射调用对应方法。</summary>
        /// <param name="key">处理器 Key（表中 action_key）</param>
        /// <param name="context">触发上下文（若方法含 PlcEventContext 首参数则注入）</param>
        /// <returns>是否找到并成功发起调用（未注册返回 false）</returns>
        public async Task<bool> InvokeAsync(string? key, PlcEventContext context)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!_descriptors.TryGetValue(key, out var desc)) return false;

            object?[] args = desc.HasContextParam ? new object?[] { context } : Array.Empty<object?>();

            try
            {
                var task = (Task)desc.Method.Invoke(desc.Host, args)!;
                await task.ConfigureAwait(false);
                return true;
            }
            catch (TargetInvocationException tie)
            {
                var ex = tie.InnerException ?? tie;
                _logger.LogError(ex, "[PLC事件] 处理器 {Key}（{Type}.{Method}）执行异常",
                    key, desc.DeclaringType.Name, desc.MethodName);
                return false;
            }
        }
    }
}
