using mcpx_template_project.Domain.Enums;
using mcpx_template_project.Domain.Model.Plc;
using mcpx_template_project.Domain.VO;
using mcpx_template_project.Helper;

namespace mcpx_template_project.Core.Plc
{
    /// <summary>
    /// 业务点位构建工具：把某个业务类型的读写计划（<see cref="PlcObjectPlan"/>）
    /// 展开为一组 <see cref="PointValueVO"/>，供通讯引擎在连接时注入缓存。
    /// 真实与 Mock 通讯引擎共用，保证业务点位在两个实现下行为一致。
    /// </summary>
    public static class PlcBusinessPointBuilder
    {
        /// <summary>
        /// 把一个业务分组（类）的读写计划展开为点位列表。
        /// 每个点位：SettingName=通讯设定名、AreaType=类名、Quantity=1、ClrType=字段类型。
        /// 读点与写点按 (设备代号, 地址) 去重（同地址读写合并为一条，读点优先保留类型）。
        /// </summary>
        /// <param name="settingId">通讯设定ID</param>
        /// <param name="settingName">通讯设定名称</param>
        /// <param name="group">业务分组（含类型名与读写计划）</param>
        public static List<PointValueVO> Build(long settingId, string settingName, PlcBusinessPointGroup group)
        {
            var plan = group.Plan;
            var typeName = group.TypeName;

            // 地址去重：key = "设备代号:地址"（忽略大小写）
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var points = new List<PointValueVO>();

            // 读点（优先，保证同地址合并时保留读点类型语义）
            foreach (var read in plan.Reads)
            {
                AddPoint(points, seen, settingId, settingName, typeName, read.DeviceCode, read.Address, read.DataType, read.PropertyName);
            }

            // 写值点（普通值）
            foreach (var write in plan.WriteValues)
            {
                AddPoint(points, seen, settingId, settingName, typeName, write.DeviceCode, write.Address, write.DataType, write.PropertyName);
            }

            // 触发位 / 回复位
            if (plan.Trigger != null)
                AddPoint(points, seen, settingId, settingName, typeName, plan.Trigger.DeviceCode, plan.Trigger.Address, plan.Trigger.DataType, plan.Trigger.PropertyName);
            if (plan.Reply != null)
                AddPoint(points, seen, settingId, settingName, typeName, plan.Reply.DeviceCode, plan.Reply.Address, plan.Reply.DataType, plan.Reply.PropertyName);

            return points;
        }

        private static void AddPoint(
            List<PointValueVO> points,
            HashSet<string> seen,
            long settingId,
            string settingName,
            string typeName,
            PlcDevice deviceCode,
            string address,
            Type dataType,
            string propertyName)
        {
            var code = deviceCode.ToString();
            if (!seen.Add(code + ":" + address)) return;

            bool isBit = PlcHelper.IsBitDevice(code);

            points.Add(new PointValueVO
            {
                SettingId = settingId,
                SettingName = settingName,
                AreaType = typeName,
                PropertyName = propertyName,
                DeviceCode = code,
                PlcAddress = address,
                Quantity = 1,
                Value = string.Empty,
                IsOnline = false,
                ErrorMessage = "等待读取",
                DataType = isBit ? "位(bit)" : "字(word)",
                ClrType = dataType,
                UpdateTime = DateTime.Now,
                Values = { new PointSubValueVO { DeviceCode = code, PlcAddress = address, PropertyName = propertyName, Value = string.Empty } }
            });
        }
    }
}
