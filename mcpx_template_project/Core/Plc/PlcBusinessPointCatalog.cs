using System.Collections.Concurrent;
using System.Reflection;
using mcpx_template_project.Aspect.Attributes;
using mcpx_template_project.Core.Scanner;
using mcpx_template_project.Domain.Model.Plc;

namespace mcpx_template_project.Core.Plc
{
    /// <summary>
    /// 某个业务类型在监控中呈现的一个分组描述。
    /// </summary>
    public sealed class PlcBusinessPointGroup
    {
        /// <summary>承载注解的业务类型</summary>
        public Type OwnerType { get; init; } = null!;

        /// <summary>展示名（区域列显示的类名）</summary>
        public string TypeName => OwnerType.Name;

        /// <summary>该类型的读写计划（读点/写值点/触发位/回复位）</summary>
        public PlcObjectPlan Plan { get; init; } = new();
    }

    /// <summary>
    /// PLC 业务点位目录：扫描程序集内所有带 [PlcRead] / [PlcWrite] 注解的类，
    /// 构建「业务类型 → 读写计划」列表。通讯引擎在建立连接时据此为每个通讯设定
    /// 统一注入业务点位（如 Recipe / MachinePlc），使监控模块能展示这些
    /// 注解声明的点位（真实与 Mock 实现共用）。
    /// </summary>
    public sealed class PlcBusinessPointCatalog
    {
        private readonly ConcurrentDictionary<Assembly, IReadOnlyList<PlcBusinessPointGroup>> _cache = new();

        /// <summary>
        /// 扫描程序集，返回所有带 PLC 注解的业务类型分组。
        /// 仅收集「同时声明了 [PlcRead] 或 [PlcWrite]」的类型，排除纯 POCO。
        /// </summary>
        public IReadOnlyList<PlcBusinessPointGroup> Scan(Assembly assembly)
        {
            return _cache.GetOrAdd(assembly, a => ScanCore(a));
        }

        private static IReadOnlyList<PlcBusinessPointGroup> ScanCore(Assembly assembly)
        {
            var groups = new List<PlcBusinessPointGroup>();

            foreach (var type in assembly.GetTypes())
            {
                // 跳过抽象/接口/泛型定义，避免无效扫描
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                    continue;

                bool hasPlcAnnotation = false;
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.GetCustomAttribute<PlcReadAttribute>() != null ||
                        prop.GetCustomAttribute<PlcWriteAttribute>() != null)
                    {
                        hasPlcAnnotation = true;
                        break;
                    }
                }

                if (!hasPlcAnnotation) continue;

                groups.Add(new PlcBusinessPointGroup
                {
                    OwnerType = type,
                    Plan = PlcPointAttributeScanner.Scan(type)
                });
            }

            // 稳定排序：按类型名
            return groups
                .OrderBy(g => g.TypeName, StringComparer.Ordinal)
                .ToList();
        }
    }
}
