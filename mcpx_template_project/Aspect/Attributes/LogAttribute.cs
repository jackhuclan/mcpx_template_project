using System.Diagnostics;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using MethodBoundaryAspect.Fody.Attributes;
using Secs4Net;
using Secs4Net.Sml;
using mcpx_template_project.Core.Logging;
using mcpx_template_project.Domain.Entities.System;
using mcpx_template_project.Domain.Enums;
using mcpx_template_project.Helper;
using mcpx_template_project.Service.Admin;

namespace mcpx_template_project.Aspect.Attributes
{
    /// <summary>
    /// 日志拦截特性
    /// </summary>
    /// <author>Rudeus</author>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property,
                AllowMultiple = false, Inherited = true)]
    public sealed class LogAttribute : OnMethodBoundaryAspect
    {
        // 模块标题
        public string Title { get; set; } = "";
        // 业务类型
        public BusinessType BusinessType { get; set; } = BusinessType.Other;
        // 操作类型
        public OperatorType OperatorType { get; set; } = OperatorType.Desktop;
        // 是否保存请求参数
        public bool IsSaveRequestData { get; set; } = true;
        // 是否保存返回结果
        public bool IsSaveResponseData { get; set; } = true;
        // 排除的参数名（逗号分隔）
        public string ExcludeParamNames { get; set; } = "";

        private string _methodName = string.Empty;
        private string _className = string.Empty;
        private Stopwatch? _stopwatch;

        /// <summary>
        /// 序列化选项：中文等非 ASCII 字符不转义，直接以原文写入数据库
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// 日志内容最大长度（防止极端情况下无限膨胀）
        /// </summary>
        private const int MaxLogContentLength = 100_000;

        /// <summary>
        /// 通用接口入口切面
        /// </summary>
        public override void OnEntry(MethodExecutionArgs args)
        {
            _className = args.Method.DeclaringType?.Name ?? "Unknown";
            _methodName = args.Method.Name;

            var logService = AppHelper.GetService<ILogService>();
            logService?.LogInformation("[AOP-ENTER] {Class}.{Method} 参数: {Params}",
                _className, _methodName, GetRequestParamsJson(args));

            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// 通用接口出口切面 —— 构建操作日志并异步入库
        /// </summary>
        public override void OnExit(MethodExecutionArgs args)
        {
            _stopwatch?.Stop();
            var elapsed = _stopwatch?.ElapsedMilliseconds ?? 0;

            var logService = AppHelper.GetService<ILogService>();
            logService?.LogInformation("[AOP-EXIT] {Class}.{Method} - 耗时: {Elapsed}ms, 返回值: {Result}",
                _className, _methodName, elapsed, GetReturnValueJson(args));

            // 构建操作日志并异步保存
            var operLog = BuildOperLog(args, elapsed, null);
            SaveOperLogFireAndForget(operLog);
        }

        /// <summary>
        /// 通用异常捕获 —— 构建带错误信息的操作日志并异步入库
        /// </summary>
        public override void OnException(MethodExecutionArgs args)
        {
            _stopwatch?.Stop();
            var elapsed = _stopwatch?.ElapsedMilliseconds ?? 0;

            var logService = AppHelper.GetService<ILogService>();
            logService?.LogError(args.Exception, $"[AOP-EXCEPTION] {_className}.{_methodName} - 耗时: {elapsed}ms");

            // 构建带异常的操作日志
            var operLog = BuildOperLog(args, elapsed, args.Exception);
            SaveOperLogFireAndForget(operLog);
        }

        /// <summary>
        /// 构建 SysOperLog 对象
        /// </summary>
        private SysOperLog BuildOperLog(MethodExecutionArgs args, long elapsed, Exception? ex)
        {
            var operLog = new SysOperLog
            {
                Title = Title,
                BusinessType = (int)BusinessType,
                OperatorType = (int)OperatorType,
                Method = $"{_className}.{_methodName}()",
                CostTime = elapsed,
                Status = ex != null ? (int)BusinessStatus.Fail : (int)BusinessStatus.Success
            };

            // 异常信息
            if (ex != null)
            {
                operLog.ErrorMsg = Truncate(ex.Message, MaxLogContentLength);
            }

            // 请求参数
            if (IsSaveRequestData)
            {
                operLog.OperParam = GetRequestParamsJson(args);
            }

            // 返回结果（异步方法的 ReturnValue 是内部状态机类型，跳过序列化）
            if (IsSaveResponseData && ex == null && args.ReturnValue != null)
            {
                operLog.JsonResult = GetReturnValueJson(args);
            }

            return operLog;
        }

        /// <summary>
        /// 获取并序列化请求参数为 JSON 字符串（排除指定参数名）。
        /// 用于数据库 OperParam 字段与日志打印，二者保持一致。
        /// </summary>
        private string GetRequestParamsJson(MethodExecutionArgs args)
        {
            var excludeSet = new HashSet<string>(
                ExcludeParamNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim()));

            var parameters = args.Method.GetParameters();
            var paramDict = new Dictionary<string, object?>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = parameters[i].Name;
                if (!string.IsNullOrEmpty(paramName) && !excludeSet.Contains(paramName))
                {
                    // 逐个值先转成可序列化对象，避免 SecsMessage/CancellationToken 在字典里导致整体序列化失败
                    paramDict[paramName] = ToSerializableObject(args.Arguments[i]);
                }
            }

            return SerializeSafe(paramDict);
        }

        /// <summary>
        /// 获取返回值的 JSON 字符串。
        /// async 方法的 ReturnValue 是 Task：已完成的 Task 取出 Result 序列化；未完成/状态机类型降级为文本。
        /// </summary>
        private string GetReturnValueJson(MethodExecutionArgs args)
        {
            var returnValue = args.ReturnValue;
            if (returnValue == null) return "null";

            var typeName = returnValue.GetType().FullName ?? string.Empty;

            // 编译器生成的 async 状态机内部类型（非公开），直接跳过
            if (typeName.Contains("AsyncStateMachine") || typeName.Contains("VoidTaskResult"))
                return string.Empty;

            // Task / Task<T>：取真实结果
            if (returnValue is Task task)
            {
                if (!task.IsCompleted)
                    return Truncate($"<未完成的任务: {typeName}>", MaxLogContentLength);

                // Task<T> 用反射取 Result（避免引入动态类型判断的复杂性）
                var resultProp = returnValue.GetType().GetProperty("Result");
                if (resultProp != null && returnValue.GetType().IsGenericType)
                {
                    try
                    {
                        var result = resultProp.GetValue(returnValue);
                        return result == null ? "null" : SerializeSafe(result);
                    }
                    catch
                    {
                        // 取 Result 异常（如任务取消）时降级
                        return Truncate($"<任务已{task.Status}: {typeName}>", MaxLogContentLength);
                    }
                }

                // 非泛型 Task（无返回值）
                return Truncate($"<已完成: {typeName}>", MaxLogContentLength);
            }

            return SerializeSafe(returnValue);
        }

        /// <summary>
        /// 统一安全 JSON 序列化：优先 System.Text.Json，失败时（含 Memory/Span、CancellationToken 等）
        /// 降级为对象文本，绝不抛异常。数据库与日志共用，保证明细一致。
        /// </summary>
        private static string SerializeSafe(object? value)
        {
            if (value is null) return "null";

            // 先转换为可序列化对象，再统一序列化
            object? serializable = ToSerializableObject(value);

            try
            {
                return Truncate(JsonSerializer.Serialize(serializable, SerializerOptions), MaxLogContentLength);
            }
            catch
            {
                try
                {
                    return Truncate(value.ToString() ?? value.GetType().Name, MaxLogContentLength);
                }
                catch
                {
                    return value.GetType().Name;
                }
            }
        }

        /// <summary>
        /// 将不可直接序列化的类型转为可序列化对象：
        /// SecsMessage → { Stream, Function, ReplyExpected, Name, Sml }；
        /// CancellationToken → "none"/"canceled"；其余原样返回。
        /// </summary>
        private static object? ToSerializableObject(object? value)
        {
            if (value is null) return null;

            if (value is Secs4Net.SecsMessage secs)
            {
                return new Dictionary<string, object?>
                {
                    ["Stream"] = secs.S,
                    ["Function"] = secs.F,
                    ["ReplyExpected"] = secs.ReplyExpected,
                    ["Name"] = string.IsNullOrEmpty(secs.Name) ? $"S{secs.S}F{secs.F}" : secs.Name,
                    ["Sml"] = secs.ToSml()
                };
            }

            if (value is CancellationToken ct)
            {
                return ct.IsCancellationRequested ? "canceled" : "none";
            }

            return value;
        }

        /// <summary>
        /// 异步入库（fire-and-forget，不阻塞主流程）
        /// </summary>
        private void SaveOperLogFireAndForget(SysOperLog operLog)
        {
            var operLogService = AppHelper.GetService<IOperLogService>();
            if (operLogService == null) return;

            _ = operLogService.SaveOperLogAsync(operLog);
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}