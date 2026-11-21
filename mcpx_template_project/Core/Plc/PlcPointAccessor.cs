using System.Linq.Expressions;
using System.Reflection;
using mcpx_template_project.Core.Scanner;
using mcpx_template_project.Domain.Model.Plc;

namespace mcpx_template_project.Core.Plc
{
    /// <summary>
    /// 业务对象属性访问器：编译一次属性 get/set 委托，运行期零反射读写属性值。
    /// getter 供通用写门面「逐字段下发」时读取业务对象的属性当前值；
    /// setter 供强类型读门面「读回填值」时把缓存值写回业务对象属性。
    /// </summary>
    /// <author>Rudeus</author>
    public sealed class PlcPointAccessor<T> where T : class
    {
        private readonly IReadOnlyDictionary<string, Func<T, object?>> _getters;
        private readonly IReadOnlyDictionary<string, Action<T, object?>> _setters;

        /// <summary>该类型的读写计划（注解扫描缓存结果）</summary>
        public PlcObjectPlan Plan { get; }

        public PlcPointAccessor()
        {
            Plan = PlcPointAttributeScanner.Scan<T>();
            _getters = BuildGetters(typeof(T));
            _setters = BuildSetters(typeof(T));
        }

        /// <summary>读取业务对象某个属性的当前值（供写回下发）。</summary>
        public object? GetValue(T obj, string propertyName)
        {
            return _getters.TryGetValue(propertyName, out var getter)
                ? getter(obj)   // 执行(T instance) => (object)instance.PropName
                : null;
        }

        /// <summary>把值写回业务对象某个属性（供读回填值）。</summary>
        public void SetValue(T obj, string propertyName, object? value)
        {
            if (_setters.TryGetValue(propertyName, out var setter))
                setter(obj, value); // 执行(T instance, object v) => instance.PropName = (PropType)v
        }

        private static IReadOnlyDictionary<string, Func<T, object?>> BuildGetters(Type type)
        {
            var dict = new Dictionary<string, Func<T, object?>>(StringComparer.Ordinal);

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                // 入参：(T instance)
                var instance = Expression.Parameter(typeof(T), "instance");
                // 访问的属性：instance.PropName
                var access = Expression.Property(instance, prop);
                // 强制类型转换：(object)instance
                var cast = Expression.Convert(access, typeof(object));
                // 打包为Lambda并编译为委托：(T instance) => (object)instance.PropName
                dict[prop.Name] = Expression.Lambda<Func<T, object?>>(cast, instance).Compile();
            }

            return dict;
        }

        private static IReadOnlyDictionary<string, Action<T, object?>> BuildSetters(Type type)
        {
            var dict = new Dictionary<string, Action<T, object?>>(StringComparer.Ordinal);

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanWrite) continue;

                // 入参：(T instance, object value)
                var instance = Expression.Parameter(typeof(T), "instance");
                var value = Expression.Parameter(typeof(object), "value");
                // 目标属性：instance.PropName
                var access = Expression.Property(instance, prop);
                // 值转换：(PropType)value
                var convert = Expression.Convert(value, prop.PropertyType);
                // 赋值：instance.PropName = (PropType)value
                var assign = Expression.Assign(access, convert);
                // (T instance, object value) => instance.PropName = (PropType)value
                dict[prop.Name] = Expression.Lambda<Action<T, object?>>(assign, instance, value).Compile();
            }

            return dict;
        }
    }
}
