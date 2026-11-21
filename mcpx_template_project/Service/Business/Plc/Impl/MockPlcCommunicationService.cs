using System.Collections.Concurrent;
using mcpx_template_project.Converters.Plc;
using mcpx_template_project.Core.Plc;
using mcpx_template_project.Core.Scanner;
using mcpx_template_project.Domain.Enums;
using mcpx_template_project.Domain.Model.Plc;
using mcpx_template_project.Domain.VO;
using mcpx_template_project.Helper;
using RecipeEntity = mcpx_template_project.Domain.Entities.Business.Recipe.Recipe;

namespace mcpx_template_project.Service.Business.Plc.Impl
{
    /// <summary>
    /// PLC 通讯引擎 Mock 实现：不建立任何真实网络连接，
    /// 用于在 debug 阶段预览实时监控页面的界面效果与交互。
    /// ConnectAsync 直接返回 true，连接时生成一次固定的模拟点位值（位/字均恒为 0）。
    /// ReadAllPointsAsync 仅返回缓存快照，不再重新赋值，避免覆盖模拟写入。
    /// 额外内置配方点位（写点/读点/触发位/成功位）与配方写入流程的自动模拟，
    /// 便于在没有真实 PLC 时测试配方下发效果。
    /// </summary>
    /// <author>Rudeus</author>
    public class MockPlcCommunicationService : IPlcCommunicationService
    {
        /// <summary>各通讯设定的连接状态标记</summary>
        private readonly ConcurrentDictionary<long, byte> _connected = new();

        /// <summary>各通讯设定对应的暂存点位列表</summary>
        private readonly ConcurrentDictionary<long, List<PointValueVO>> _cache = new();

        /// <summary>各通讯设定配方写点的最新写入值（写地址 → 值）</summary>
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, string>> _recipeWriteValues = new();

        /// <summary>
        /// 各通讯设定的地址值表（唯一真源）：key = "设备代号:地址"。
        /// 区域配置点位与业务点位共享同一地址时，读写都走这张表，保证同地址值一致。
        /// </summary>
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, string>> _addressValues = new();

        /// <summary>随机数生成器（线程安全）</summary>
        private readonly Random _random = new();

        /// <summary>位设备集合：这些 Prefix 以位(bit)为单位读取</summary>
        private static readonly HashSet<string> BitDevices = new(StringComparer.OrdinalIgnoreCase)
        {
            "X", "Y", "M", "L", "F", "V", "B", "S", "SM", "SB", "DX", "DY",
            "TS", "TC", "SS", "SC", "CS", "CC"
        };

        /// <summary>业务点位目录（扫描带 [PlcRead]/[PlcWrite] 注解的类）</summary>
        private readonly IReadOnlyList<PlcBusinessPointGroup> _businessGroups;

        /// <summary>配方实体的读写计划（注解扫描，缓存一次）</summary>
        private static readonly PlcObjectPlan RecipePlan =
            PlcPointAttributeScanner.Scan<RecipeEntity>();

        /// <summary>配方业务写值字段</summary>
        private static readonly IReadOnlyList<PlcWriteDefinition> RecipeFields = RecipePlan.WriteValues;

        /// <summary>配方写入触发位定义（M2804）</summary>
        private static readonly PlcWriteDefinition? RecipeTrigger = RecipePlan.Trigger;

        /// <summary>配方回复位定义（M2101，0/1/2）</summary>
        private static readonly PlcWriteDefinition? RecipeReply = RecipePlan.Reply;

        /// <summary>Mock 永不失败，LastError 恒为 null</summary>
        public string? LastError => null;

        /// <summary>Mock 实现：模拟模式</summary>
        public bool IsSimulation => true;

        public MockPlcCommunicationService(PlcBusinessPointCatalog catalog)
        {
            _businessGroups = catalog.Scan(typeof(MockPlcCommunicationService).Assembly);
        }

        /// <summary>
        /// 模拟建立连接：不做任何网络操作，直接返回成功并预构建点位列表。
        /// </summary>
        public Task<bool> ConnectAsync(CommSettingVO setting)
        {
            if (setting == null) return Task.FromResult(false);

            _connected[setting.Id] = 1;
            var points = BuildPointList(setting);
            _cache[setting.Id] = points;

            // 注入业务点位（带注解的类：Recipe / MachinePlc 等）
            InjectBusinessPoints(setting.Id, setting.Name, points);

            // 业务点位初始化为在线占位值（位 "0" / 字 "0"），
            // 机台点位随后由机台轮询回填真实模拟值，配方点位由写模拟回填
            foreach (var p in points)
            {
                if (!p.IsBusinessPoint) continue;
                p.Value = "0";
                p.IsOnline = true;
                p.ErrorMessage = string.Empty;
                if (p.Values.Count > 0) p.Values[0].Value = "0";
            }

            // 立即生成一次模拟值（业务点除外，其值由机台轮询/配方写模拟回填）
            RefreshMockValues(setting.Id);

            return Task.FromResult(true);
        }

        /// <summary>
        /// 模拟断开连接：保留暂存数据但标记为离线。
        /// </summary>
        public void Disconnect(long settingId)
        {
            _connected.TryRemove(settingId, out _);

            if (_cache.TryGetValue(settingId, out var points))
            {
                foreach (var p in points)
                {
                    p.IsOnline = false;
                    p.Value = string.Empty;
                    p.Values.Clear();
                    p.ErrorMessage = "连接已断开";
                    p.UpdateTime = DateTime.Now;
                }
            }
        }

        /// <summary>检查指定通讯设定是否已连接。</summary>
        public bool IsConnected(long settingId) => _connected.ContainsKey(settingId);

        /// <summary>
        /// 模拟写入单个点位：更新缓存中对应子值，返回成功。
        /// 无真实 PLC，写操作直接落到暂存缓存。
        /// 额外处理配方相关写入：写 M2804 触发配方写入模拟，写配方写点记录最新值。
        /// </summary>
        public Task<bool> WriteSinglePointAsync(long settingId, string deviceCode, string plcAddress, object value)
        {
            if (!_cache.ContainsKey(settingId))
                return Task.FromResult(false);

            var text = PlcValueConverter.ToText(value);

            // 写入统一地址值表，并同步到覆盖该地址的所有点位（区域 + 业务）
            SetAddressValue(settingId, deviceCode, plcAddress, text);
            bool updated = true;

            // 配方写点：记录最新写入值
            if (IsRecipeWriteAddress(deviceCode, plcAddress))
            {
                var dict = _recipeWriteValues.GetOrAdd(settingId, _ => new ConcurrentDictionary<string, string>());
                dict[plcAddress] = text;
            }

            // 配方触发位 M2804 置 ON：启动配方写入模拟（PLC 内部处理）
            if (IsRecipeTriggerOn(deviceCode, plcAddress, value))
            {
                _ = SimulateRecipeWriteAsync(settingId);
            }

            return Task.FromResult(updated || IsRecipeAddress(deviceCode, plcAddress));
        }

        /// <summary>
        /// 批量读取指定通讯设定所有区域配置的点位快照。
        /// 模拟值仅在连接时生成一次，此后只返回缓存、不重新赋值，
        /// 避免模拟写入的值被轮询覆盖。
        /// </summary>
        public Task<List<PointValueVO>> ReadAllPointsAsync(long settingId)
        {
            if (!_connected.ContainsKey(settingId))
                return Task.FromResult(new List<PointValueVO>());

            var points = _cache.TryGetValue(settingId, out var list)
                ? list.ToList()
                : new List<PointValueVO>();

            return Task.FromResult(points);
        }

        /// <summary>
        /// 获取所有已连接通讯设定的暂存点位数据快照。
        /// </summary>
        public List<PointValueVO> GetCachedPoints()
        {
            var all = new List<PointValueVO>();
            foreach (var kvp in _cache)
                all.AddRange(kvp.Value);
            return all;
        }

        /// <summary>
        /// 获取指定通讯设定的暂存点位列表（返回内部引用，仅读遍历）。
        /// </summary>
        public List<PointValueVO> GetCachedPoints(long settingId)
        {
            return _cache.TryGetValue(settingId, out var list) ? list : new List<PointValueVO>();
        }

        /// <summary>
        /// 从缓存读取指定通讯设定下某个 (设备代号, 地址) 的当前值文本，不发请求。
        /// 优先从统一地址值表读，未命中则从点位缓存读。
        /// </summary>
        public string? GetCachedValue(long settingId, string deviceCode, string address)
        {
            // 优先查统一地址值表（唯一真源）
            if (_addressValues.TryGetValue(settingId, out var dict)
                && dict.TryGetValue($"{deviceCode}:{address}", out var cached))
                return cached;

            if (!_cache.TryGetValue(settingId, out var points)) return null;
            foreach (var point in points)
            {
                if (!string.Equals(point.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var sub in point.Values)
                {
                    if (string.Equals(sub.PlcAddress, address, StringComparison.OrdinalIgnoreCase))
                        return sub.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取所有已连接的通讯设定ID。
        /// </summary>
        public List<long> GetConnectedSettingIds() => _connected.Keys.ToList();

        /// <summary>
        /// 更新缓存中某点位的文本值（仅内存缓存，不向 PLC 下发）。
        /// 若该点位不存在则自动补建（upsert），保证机台等固定点位无需依赖区域配置即可被缓存读取。
        /// </summary>
        public void SetCachedValue(long settingId, string deviceCode, string plcAddress, string textValue)
        {
            // 走统一地址值表并同步到所有覆盖该地址的点位（含业务点位），保证同地址一致
            if (_cache.ContainsKey(settingId))
            {
                SetAddressValue(settingId, deviceCode, plcAddress, textValue);
                return;
            }

            // 缓存尚未建立（如连接前的机台轮询回填）：补建单条点位
            var points = _cache.GetOrAdd(settingId, _ => new List<PointValueVO>());
            points.Add(new PointValueVO
            {
                SettingId = settingId,
                SettingName = string.Empty,
                AreaType = "MACHINE",
                DeviceCode = deviceCode,
                PlcAddress = plcAddress,
                Quantity = 1,
                Value = textValue,
                IsOnline = true,
                ErrorMessage = string.Empty,
                DataType = "字(word)",
                UpdateTime = DateTime.Now,
                Values =
                {
                    new PointSubValueVO { DeviceCode = deviceCode, PlcAddress = plcAddress, Value = textValue }
                }
            });
        }

        // ============ 配方模拟 ============

        /// <summary>判断是否为配方写地址</summary>
        private static bool IsRecipeWriteAddress(string deviceCode, string address)
        {
            if (!string.Equals(deviceCode, nameof(PlcDevice.D), StringComparison.OrdinalIgnoreCase)) return false;
            return RecipeFields.Any(f => f.Address == address);
        }

        /// <summary>判断是否为配方相关地址（写点/读点/触发位/回复位）</summary>
        private static bool IsRecipeAddress(string deviceCode, string address)
        {
            if (RecipeTrigger != null &&
                string.Equals(deviceCode, RecipeTrigger.DeviceCode.ToString(), StringComparison.OrdinalIgnoreCase))
                return address == RecipeTrigger.Address
                    || (RecipeReply != null && address == RecipeReply.Address);
            if (!string.Equals(deviceCode, nameof(PlcDevice.D), StringComparison.OrdinalIgnoreCase)) return false;
            return RecipeFields.Any(f => f.Address == address)
                || RecipePlan.Reads.Any(r => r.Address == address);
        }

        /// <summary>判断是否写入了配方触发位 M2804 且值为 ON</summary>
        private static bool IsRecipeTriggerOn(string deviceCode, string address, object value)
        {
            if (RecipeTrigger == null) return false;
            if (!string.Equals(deviceCode, RecipeTrigger.DeviceCode.ToString(), StringComparison.OrdinalIgnoreCase)) return false;
            if (address != RecipeTrigger.Address) return false;
            return value is bool b ? b : string.Equals(PlcValueConverter.ToText(value), "ON", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 模拟配方写入流程（三态握手）：
        /// 延迟后把写点值回写到读点，回复位置 ACK(1)；
        /// 等待 PC 将触发位复归 0 后，回复位也复归 0（由 PC 写 0，本模拟配合 PC 侧复归）。
        /// 模拟「PLC 内部处理完成后，将实际配方值回写到读点并应答」的行为。
        /// </summary>
        private async Task SimulateRecipeWriteAsync(long settingId)
        {
            // 模拟 PLC 处理耗时
            await Task.Delay(800);

            if (!_cache.TryGetValue(settingId, out var points)) return;

            var writeValues = _recipeWriteValues.GetOrAdd(settingId, _ => new ConcurrentDictionary<string, string>());

            // 把写点最新值回写到对应读点
            foreach (var field in RecipeFields)
            {
                if (!writeValues.TryGetValue(field.Address, out var val)) continue;
                var readDef = RecipePlan.Reads.FirstOrDefault(r => r.PropertyName == field.PropertyName);
                if (readDef == null) continue;
                SetAddressValue(settingId, readDef.DeviceCode.ToString(), readDef.Address, val);
            }

            // 回复位置 ACK(1)
            if (RecipeReply != null)
                SetAddressValue(settingId, RecipeReply.DeviceCode.ToString(), RecipeReply.Address, "1");

            // 等待 PC 将触发位复归 0（回复位复归由 PC 主动写 0 完成，本模拟仅在此等待触发位回 0）
            if (RecipeReply != null && RecipeTrigger != null)
            {
                _ = Task.Run(async () =>
                {
                    // 轮询触发位，直到 PC 置回 0
                    var deadline = DateTime.Now.AddSeconds(10);
                    while (DateTime.Now < deadline)
                    {
                        var trigger = RecipeTrigger.DeviceCode.ToString();
                        if (TriggerIsOff(points, trigger, RecipeTrigger.Address))
                            break;
                        await Task.Delay(100);
                    }
                });
            }
        }

        /// <summary>判断缓存中触发位是否为「OFF/0/false」。</summary>
        private static bool TriggerIsOff(List<PointValueVO> points, string deviceCode, string address)
        {
            foreach (var point in points)
            {
                if (!string.Equals(point.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var sub in point.Values)
                {
                    if (string.Equals(sub.PlcAddress, address, StringComparison.OrdinalIgnoreCase))
                        return !PlcHelper.IsOn(sub.Value);
                }
            }
            return true; // 未找到视为已复位，避免死循环
        }

        /// <summary>
        /// 注入业务点位：遍历业务点位目录，把每个带注解类的读写计划展开为点位
        /// （AreaType=类名，ClrType=字段类型，SettingName=通讯设定名）。
        /// 与真实引擎共用 <see cref="PlcBusinessPointBuilder"/>，行为一致。
        /// </summary>
        private void InjectBusinessPoints(long settingId, string settingName, List<PointValueVO> points)
        {
            foreach (var group in _businessGroups)
            {
                points.AddRange(PlcBusinessPointBuilder.Build(settingId, settingName, group));
            }
        }

        // ============ 原有逻辑 ============

        /// <summary>
        /// 根据通讯设定的区域配置，预构建点位值列表。
        /// </summary>
        private static List<PointValueVO> BuildPointList(CommSettingVO setting)
        {
            var points = new List<PointValueVO>();
            if (setting.Areas == null) return points;

            foreach (var area in setting.Areas)
            {
                points.Add(new PointValueVO
                {
                    SettingId = setting.Id,
                    SettingName = setting.Name,
                    AreaType = area.AreaType,
                    DeviceCode = area.Name,
                    PlcAddress = area.Position,
                    Quantity = area.Quantity,
                    Value = string.Empty,
                    IsOnline = false,
                    ErrorMessage = "等待读取",
                    UpdateTime = DateTime.Now
                });
            }
            return points;
        }

        /// <summary>
        /// 为指定通讯设定的所有区域配置点位生成模拟值，写入统一地址值表并同步到所有覆盖该地址的点位。
        /// 位设备与字设备均生成固定值 0（不再随机）。
        /// 仅在连接时调用一次，轮询不再调用，避免覆盖模拟写入。
        /// 业务点位不在此生成；其地址若与区域点位重叠，则从值表读到同一值，保证一致。
        /// </summary>
        private void RefreshMockValues(long settingId)
        {
            if (!_cache.TryGetValue(settingId, out var points)) return;

            foreach (var point in points)
            {
                if (point.IsBusinessPoint) continue; // 业务点不在此生成

                var quantity = Math.Max(point.Quantity, 1);
                bool isBit = BitDevices.Contains(point.DeviceCode ?? string.Empty);

                // 生成固定值数组并展开区域点位子值（保证 Values 地址正确）。
                // 模拟模式不再随机：位设备恒为 "0"，字设备恒为 "0"，值不随刷新周期变化。
                var textValues = new string[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    textValues[i] = isBit ? "0" : "0";
                }
                UpdateSubValues(point, textValues);
                point.Value = string.Join(", ", textValues);
                point.DataType = isBit ? "位(bit)" : "字(word)";
                point.IsOnline = true;
                point.ErrorMessage = string.Empty;
                point.UpdateTime = DateTime.Now;

                // 逐个写入统一值表并同步到覆盖该地址的所有点位（含业务点位，保证同地址一致）
                foreach (var sub in point.Values)
                {
                    SetAddressValue(settingId, point.DeviceCode ?? string.Empty, sub.PlcAddress ?? string.Empty, sub.Value ?? string.Empty);
                }
            }
        }

        /// <summary>
        /// 把某个 (设备代号, 地址) 的值写入统一地址值表，并同步到覆盖该地址的所有点位子值。
        /// 这是 Mock 值更新的唯一入口：区域随机、机台轮询、配方写模拟、单点写入都经由此处，
        /// 保证区域点位与业务点位对同一地址读到同一值。
        /// </summary>
        private void SetAddressValue(long settingId, string deviceCode, string address, string value)
        {
            var dict = _addressValues.GetOrAdd(settingId, _ => new ConcurrentDictionary<string, string>());
            dict[$"{deviceCode}:{address}"] = value;

            if (!_cache.TryGetValue(settingId, out var points)) return;

            foreach (var point in points)
            {
                if (!string.Equals(point.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var sub in point.Values)
                {
                    if (!string.Equals(sub.PlcAddress, address, StringComparison.OrdinalIgnoreCase)) continue;

                    sub.Value = value;
                    point.IsOnline = true;
                    point.ErrorMessage = string.Empty;
                    point.UpdateTime = DateTime.Now;

                    // 单值点位（业务点位）同步汇总 Value；多值区域点位汇总由下方统一刷新
                    if (point.Quantity == 1)
                        point.Value = value;
                }
            }
        }

        /// <summary>
        /// 根据读取到的文本值展开为子值列表（地址递增）。
        /// 地址进制按设备代号区分（D/M 等十进制、X/Y/B/W 等十六进制），与 McpX 语义一致。
        /// </summary>
        private static void UpdateSubValues(PointValueVO point, string[] textValues)
        {
            while (point.Values.Count > textValues.Length)
                point.Values.RemoveAt(point.Values.Count - 1);

            int numberBase = PlcAddressHelper.IsHexDevice(point.DeviceCode) ? 16 : 10;
            bool ok = PlcAddressHelper.TryParseAddress(point.PlcAddress, numberBase, out long startAddr);
            if (!ok) startAddr = 0;

            for (int i = 0; i < textValues.Length; i++)
            {
                var addr = PlcAddressHelper.FormatAddress(startAddr + i, numberBase);
                if (i < point.Values.Count)
                {
                    point.Values[i].DeviceCode = point.DeviceCode;
                    point.Values[i].PlcAddress = addr;
                    point.Values[i].Value = textValues[i];
                }
                else
                {
                    point.Values.Add(new PointSubValueVO
                    {
                        DeviceCode = point.DeviceCode,
                        PlcAddress = addr,
                        Value = textValues[i]
                    });
                }
            }
        }
    }
}
