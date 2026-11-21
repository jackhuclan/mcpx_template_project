using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using mcpx_template_project.Domain.VO;
using mcpx_template_project.Helper;
using mcpx_template_project.Service.Admin;
using mcpx_template_project.Service.Business.Plc;

namespace mcpx_template_project.ViewModels.Business.Monitor
{
    /// <summary>
    /// 实时监控视图模型：通过 DispatcherTimer 周期性批量读取所有已连接通讯设定的PLC点位，
    /// 将暂存数据实时展示在界面上。支持按「通讯设定 / 区域 / 设备代号 / PLC地址」筛选查询，
    /// 点开行详情可展开查看该区域点位下每个 PLC 地址的当前值。
    /// </summary>
    /// <author>Rudeus</author>
    public partial class PlcMonitorViewModel : ObservableObject
    {
        private readonly IPlcCommunicationService _plcService;
        private readonly IDialogService _dialogService;
        private readonly DispatcherTimer _timer;

        /// <summary>全量点位（与通讯引擎暂存缓存共享对象引用）</summary>
        private readonly List<PointValueVO> _allPoints = new();

        /// <summary>区域配置点位（AI/AO/DI/DO，绑定到上方 DataGrid）</summary>
        public ObservableCollection<PointValueVO> AreaPoints { get; } = new();

        /// <summary>业务点位（注解类 Recipe/MachinePlc 等，内部源集合，按通讯设定→类名→地址排序）</summary>
        public ObservableCollection<PointValueVO> BusinessPoints { get; } = new();

        /// <summary>业务区域聚合行（左侧业务区列表，每行=一个 通讯设定×业务类 组合，绑定下方 DataGrid）</summary>
        public ObservableCollection<BusinessAreaVO> BusinessAreas { get; } = new();

        /// <summary>区域筛选下拉选项（全部 + 区域类型 + 业务类名）</summary>
        public ObservableCollection<string> AreaTypeOptions { get; } = new();

        /// <summary>当前选中的点位单头（右侧子值明细面板绑定其 Values，来源可为区域表或业务表）</summary>
        [ObservableProperty]
        private PointValueVO? _selectedPoint;

        /// <summary>区域表当前选中项</summary>
        [ObservableProperty]
        private PointValueVO? _selectedAreaPoint;

        /// <summary>业务表当前选中项</summary>
        [ObservableProperty]
        private PointValueVO? _selectedBusinessPoint;

        /// <summary>业务区域聚合行当前选中项</summary>
        [ObservableProperty]
        private BusinessAreaVO? _selectedBusinessArea;

        /// <summary>是否正在监控中</summary>
        [ObservableProperty]
        private bool _isMonitoring;

        /// <summary>刷新周期（毫秒），默认1000ms</summary>
        [ObservableProperty]
        private int _refreshIntervalMs = 1000;

        /// <summary>已连接的通讯设定数量</summary>
        [ObservableProperty]
        private int _connectedCount;

        /// <summary>点位总数</summary>
        [ObservableProperty]
        private int _pointCount;

        /// <summary>在线点位数</summary>
        [ObservableProperty]
        private int _onlineCount;

        /// <summary>最近刷新时间</summary>
        [ObservableProperty]
        private string _lastRefreshTime = "尚未刷新";

        // ---------- 查询筛选条件 ----------

        /// <summary>通讯设定名称筛选（模糊匹配）</summary>
        [ObservableProperty]
        private string _filterSettingName = string.Empty;

        /// <summary>区域筛选（全部 / AI / AO / DI / DO / 业务类名）</summary>
        [ObservableProperty]
        private string _filterAreaType = "全部";

        /// <summary>设备代号筛选（模糊匹配）</summary>
        [ObservableProperty]
        private string _filterDeviceCode = string.Empty;

        /// <summary>PLC地址筛选（模糊匹配）</summary>
        [ObservableProperty]
        private string _filterPlcAddress = string.Empty;

        /// <summary>右侧子值明细的地址筛选（模糊匹配，作用于当前选中点位的 Values）</summary>
        [ObservableProperty]
        private string _filterSubAddress = string.Empty;

        /// <summary>右侧子值明细展示集合（按地址筛选后的当前选中点位子值）</summary>
        public ObservableCollection<PointSubValueVO> SubValues { get; } = new();

        /// <summary>已订阅 Value 变化事件的子值列表（用于取消订阅，实现模拟模式边输入边改值）</summary>
        private readonly List<PointSubValueVO> _subscribedSubValues = new();

        public PlcMonitorViewModel(IPlcCommunicationService plcService, IDialogService dialogService)
        {
            _plcService = plcService;
            _dialogService = dialogService;

            // 区域筛选下拉：全部 + 区域类型 + 业务类名（在 ApplyFilter 时动态补齐业务类名）
            AreaTypeOptions.Add("全部");
            AreaTypeOptions.Add("AI");
            AreaTypeOptions.Add("AO");
            AreaTypeOptions.Add("DI");
            AreaTypeOptions.Add("DO");

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs)
            };
            _timer.Tick += OnTimerTick;
        }

        /// <summary>刷新周期变化时同步 Timer Interval</summary>
        partial void OnRefreshIntervalMsChanged(int value)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(value, 100));
        }

        /// <summary>区域表选中项变化：联动右侧明细，并清空业务表/业务区域选中。</summary>
        partial void OnSelectedAreaPointChanged(PointValueVO? value)
        {
            if (value == null) return;
            _selectedBusinessPoint = null;
            _selectedBusinessArea = null;
            SelectedPoint = value;
        }

        /// <summary>业务区域聚合行选中：清空区域表/业务点位表选中，右侧明细展示该区域的 Children。</summary>
        partial void OnSelectedBusinessAreaChanged(BusinessAreaVO? value)
        {
            if (value == null) return;
            _selectedAreaPoint = null;
            _selectedBusinessPoint = null;
            SelectedPoint = null;
            RebuildSubValueSubscriptions();
            ApplySubFilter();
        }

        /// <summary>选中点位变化：重建子值订阅与右侧子值明细（受地址筛选约束）。</summary>
        partial void OnSelectedPointChanged(PointValueVO? value)
        {
            RebuildSubValueSubscriptions();
            ApplySubFilter();
        }

        /// <summary>子值地址筛选变化：重建右侧子值明细。</summary>
        partial void OnFilterSubAddressChanged(string value) => ApplySubFilter();

        /// <summary>
        /// 当前活跃的通讯设定ID（右侧明细写入目标）：
        /// 选中区域点位 → 该点位的 SettingId；选中业务区域 → 该区域的 SettingId。
        /// </summary>
        private long? ActiveSettingId =>
            SelectedPoint?.SettingId ?? SelectedBusinessArea?.SettingId;

        /// <summary>
        /// 当前右侧明细数据源：选中区域点位的 Values，或选中业务区域的 Children。
        /// </summary>
        private IEnumerable<PointSubValueVO> ActiveSubValueSource =>
            SelectedPoint?.Values ?? SelectedBusinessArea?.Children ?? Enumerable.Empty<PointSubValueVO>();

        /// <summary>
        /// 重建当前选中点位/业务区域的子值 Value 变化订阅：
        /// 模拟模式下，用户边输入边把值写入统一值表（SetCachedValue），
        /// 使机台/监控轮询回填的即为用户输入值，不会覆盖输入。
        /// 真实模式不订阅，仍走 Enter/按钮提交写 PLC。
        /// </summary>
        private void RebuildSubValueSubscriptions()
        {
            foreach (var sub in _subscribedSubValues)
                sub.PropertyChanged -= OnSubValueChanged;
            _subscribedSubValues.Clear();

            if (!_plcService.IsSimulation) return;

            foreach (var sub in ActiveSubValueSource)
            {
                sub.PropertyChanged += OnSubValueChanged;
                _subscribedSubValues.Add(sub);
            }
        }

        /// <summary>子值 Value 变化：模拟模式下实时写值表（纯内存，不触发配方/真实 PLC 下发）。</summary>
        private void OnSubValueChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PointSubValueVO.Value)) return;
            if (sender is not PointSubValueVO sub) return;

            var settingId = ActiveSettingId;
            if (settingId == null) return;

            var deviceCode = sub.DeviceCode ?? string.Empty;
            var plcAddress = sub.PlcAddress ?? string.Empty;
            var text = sub.Value ?? string.Empty;

            if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(plcAddress)) return;

            Serilog.Log.Information("[监控写值] settingId={SettingId} {Device}{Addr} = {Value} (业务区域={Area})",
                settingId.Value, deviceCode, plcAddress, text, SelectedBusinessArea?.AreaType ?? SelectedPoint?.AreaType);

            // 纯内存写值表，值相同时不会再次触发本事件，无递归
            _plcService.SetCachedValue(settingId.Value, deviceCode, plcAddress, text);
        }

        /// <summary>筛选条件变化时重新应用过滤</summary>
        partial void OnFilterSettingNameChanged(string value) => ApplyFilter();
        partial void OnFilterAreaTypeChanged(string value) => ApplyFilter();
        partial void OnFilterDeviceCodeChanged(string value) => ApplyFilter();
        partial void OnFilterPlcAddressChanged(string value) => ApplyFilter();

        /// <summary>
        /// 启动实时监控：开启 Timer 周期读取，立即执行一次刷新。
        /// </summary>
        [RelayCommand]
        private async Task StartMonitor()
        {
            if (IsMonitoring) return;
            IsMonitoring = true;
            _timer.Start();
            await RefreshAsync();
        }

        /// <summary>
        /// 停止实时监控：关闭 Timer。
        /// </summary>
        [RelayCommand]
        private void StopMonitor()
        {
            IsMonitoring = false;
            _timer.Stop();
        }

        /// <summary>
        /// 手动刷新一次（不影响 Timer 运行状态）。
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            try
            {
                var ids = _plcService.GetConnectedSettingIds();
                ConnectedCount = ids.Count;

                if (ids.Count == 0)
                {
                    LastRefreshTime = $"[{DateTime.Now:HH:mm:ss}] 暂无已连接的通讯设定";
                    return;
                }

                // 批量读取每个已连接通讯设定的所有点位
                foreach (var id in ids)
                {
                    await _plcService.ReadAllPointsAsync(id);
                }

                // 同步暂存缓存到全量集合（保留同一对象引用，仅做增删）
                var cached = _plcService.GetCachedPoints();
                SyncAllPoints(cached);

                ApplyFilter();

                PointCount = AreaPoints.Count + BusinessPoints.Count;
                OnlineCount = AreaPoints.Count(p => p.IsOnline) + BusinessPoints.Count(p => p.IsOnline);                LastRefreshTime = $"[{DateTime.Now:HH:mm:ss}] 刷新成功 · {PointCount} 点位 · {OnlineCount} 在线";
            }
            catch (Exception ex)
            {
                LastRefreshTime = $"[{DateTime.Now:HH:mm:ss}] 刷新失败：{ex.Message}";
            }
        }

        /// <summary>
        /// Timer Tick 回调：周期性刷新点位值。
        /// </summary>
        private async void OnTimerTick(object? sender, EventArgs e)
        {
            await RefreshAsync();
        }

        /// <summary>
        /// 将通讯引擎暂存缓存同步到全量集合：
        /// 保留同一对象引用（属性变化由 INPC 自动驱动），仅做增删操作。
        /// </summary>
        private void SyncAllPoints(List<PointValueVO> cached)
        {
            var cacheSet = new HashSet<PointValueVO>(cached);

            // 移除缓存中已不存在的点位
            for (int i = _allPoints.Count - 1; i >= 0; i--)
            {
                if (!cacheSet.Contains(_allPoints[i]))
                    _allPoints.RemoveAt(i);
            }

            // 添加缓存中新出现的点位
            var currentSet = new HashSet<PointValueVO>(_allPoints);
            foreach (var p in cached)
            {
                if (!currentSet.Contains(p))
                    _allPoints.Add(p);
            }
        }

        /// <summary>
        /// 根据当前筛选条件从全量集合中过滤出两个展示集合：
        /// AreaPoints（区域配置点位，上方表）与 BusinessPoints（业务点位，下方表，按通讯设定→类名分组）。
        /// </summary>
        private void ApplyFilter()
        {
            var setting = FilterSettingName?.Trim() ?? string.Empty;
            var area = FilterAreaType?.Trim() ?? string.Empty;
            var device = FilterDeviceCode?.Trim() ?? string.Empty;
            var plcAddr = FilterPlcAddress?.Trim() ?? string.Empty;

            // 是否按区域精确筛选（"全部"或空视为不筛选）
            bool areaFilterActive = !string.IsNullOrEmpty(area) && !area.Equals("全部", StringComparison.OrdinalIgnoreCase);

            IEnumerable<PointValueVO> baseQuery = _allPoints;

            if (!string.IsNullOrEmpty(setting))
                baseQuery = baseQuery.Where(p => p.SettingName?.Contains(setting, StringComparison.OrdinalIgnoreCase) == true);

            if (areaFilterActive)
                baseQuery = baseQuery.Where(p => p.AreaType?.Equals(area, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrEmpty(device))
                baseQuery = baseQuery.Where(p =>
                    (p.DeviceCode?.Contains(device, StringComparison.OrdinalIgnoreCase) == true) ||
                    p.Values.Any(sub => sub.DeviceCode?.Contains(device, StringComparison.OrdinalIgnoreCase) == true));

            if (!string.IsNullOrEmpty(plcAddr))
                baseQuery = baseQuery.Where(p =>
                    (p.PlcAddress?.Contains(plcAddr, StringComparison.OrdinalIgnoreCase) == true) ||
                    p.Values.Any(sub => sub.PlcAddress?.Contains(plcAddr, StringComparison.OrdinalIgnoreCase) == true));

            var filtered = baseQuery.ToList();

            // 区域配置点位（AI/AO/DI/DO）
            var areaResult = filtered
                .Where(p => !p.IsBusinessPoint)
                .OrderBy(p => p.SettingName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => GetAreaTypeOrder(p.AreaType))
                .ThenBy(p => GetAddressSortKey(p))
                .ToList();

            // 业务点位（按通讯设定 → 类名 → 地址分组排序）
            var businessResult = filtered
                .Where(p => p.IsBusinessPoint)
                .OrderBy(p => p.SettingName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.AreaType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => GetAddressSortKey(p))
                .ToList();

            SyncCollection(AreaPoints, areaResult);
            SyncCollection(BusinessPoints, businessResult);

            // 业务区域聚合：按 (通讯设定, 类名) 聚合为一行，右侧明细为该区域下所有属性点位
            SyncBusinessAreas(businessResult);

            // 动态补齐业务类名到区域筛选下拉
            SyncAreaTypeOptions();

            // 同步选中项：若原选中项被过滤掉，回退到对应表第一项。
            // 用户选中业务区域时（SelectedBusinessArea != null），区域表选中保持为空，
            // 不要自动回退到区域表第一项，否则会清空业务区域选中并覆盖右侧明细。
            if (SelectedAreaPoint != null && !AreaPoints.Contains(SelectedAreaPoint))
                SelectedAreaPoint = AreaPoints.FirstOrDefault();
            else if (SelectedAreaPoint == null && SelectedPoint == null && SelectedBusinessArea == null)
                SelectedAreaPoint = AreaPoints.FirstOrDefault();

            if (SelectedBusinessArea != null && !BusinessAreas.Contains(SelectedBusinessArea))
                SelectedBusinessArea = BusinessAreas.FirstOrDefault();

            // 若当前选中点已不在任一表中，清空右侧明细
            if (SelectedPoint != null && !AreaPoints.Contains(SelectedPoint))
                SelectedPoint = null;
        }

        /// <summary>
        /// 把业务点位按（通讯设定, 类名）聚合为 <see cref="BusinessAreaVO"/> 列表，
        /// 每个区域一行，其下所有属性点位挂到 Children（右侧明细数据源）。
        /// 聚合行对象按（SettingId, SettingName, AreaType）稳定复用，属性值由 INPC 自动刷新。
        /// </summary>
        private void SyncBusinessAreas(List<PointValueVO> businessPoints)
        {
            // 按 (SettingId, SettingName, AreaType) 分组
            var groups = businessPoints
                .GroupBy(p => (p.SettingId, p.SettingName, p.AreaType))
                .ToList();

            // 复用已有聚合行：以 (SettingId, AreaType) 为键
            var existing = new Dictionary<(long, string), BusinessAreaVO>();
            foreach (var a in BusinessAreas)
                existing[(a.SettingId, a.AreaType)] = a;

            var result = new List<BusinessAreaVO>();
            foreach (var g in groups)
            {
                var key = (g.Key.SettingId, g.Key.AreaType);
                if (!existing.TryGetValue(key, out var area))
                {
                    area = new BusinessAreaVO
                    {
                        SettingId = g.Key.SettingId,
                        SettingName = g.Key.SettingName,
                        AreaType = g.Key.AreaType,
                    };
                }

                // 重建 Children（复用子值对象引用，值由轮询 INPC 自动刷新）
                var children = new List<PointSubValueVO>();
                foreach (var p in g)
                {
                    foreach (var sub in p.Values)
                        children.Add(sub);
                }

                // 稳定排序：按 设备代号 → 地址
                children = children
                    .OrderBy(c => c.DeviceCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => GetSubAddressSortKey(c))
                    .ToList();

                SyncSubCollection(area.Children, children);

                // 聚合状态：任一子点在线即在线；错误信息取首个子点非空错误
                area.DataType = g.FirstOrDefault()?.DataType ?? string.Empty;
                area.IsOnline = g.Any(p => p.IsOnline);
                area.ErrorMessage = g.FirstOrDefault(p => !string.IsNullOrEmpty(p.ErrorMessage))?.ErrorMessage ?? string.Empty;
                area.UpdateTime = g.Max(p => p.UpdateTime);

                result.Add(area);
            }

            // 同步聚合集合：移除已不存在的，追加新的，顺序对齐
            var resultSet = new HashSet<BusinessAreaVO>(result);
            for (int i = BusinessAreas.Count - 1; i >= 0; i--)
            {
                if (!resultSet.Contains(BusinessAreas[i]))
                    BusinessAreas.RemoveAt(i);
            }
            var currentSet = new HashSet<BusinessAreaVO>(BusinessAreas);
            foreach (var a in result)
            {
                if (!currentSet.Contains(a))
                    BusinessAreas.Add(a);
            }
            if (!BusinessAreas.SequenceEqual(result))
            {
                BusinessAreas.Clear();
                foreach (var a in result) BusinessAreas.Add(a);
            }
        }

        /// <summary>子值地址排序键：优先数值，解析失败回退字符串。</summary>
        private static (int Rank, long Addr, string Text) GetSubAddressSortKey(PointSubValueVO s)
        {
            var numberBase = PlcAddressHelper.IsHexDevice(s.DeviceCode) ? 16 : 10;
            if (PlcAddressHelper.TryParseAddress(s.PlcAddress, numberBase, out long addr))
                return (0, addr, string.Empty);
            return (1, 0, s.PlcAddress ?? string.Empty);
        }

        /// <summary>同步子值集合：保留对象引用，仅增删；顺序不一致时重建。</summary>
        private static void SyncSubCollection(ObservableCollection<PointSubValueVO> target, List<PointSubValueVO> result)
        {
            var resultSet = new HashSet<PointSubValueVO>(result);
            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!resultSet.Contains(target[i]))
                    target.RemoveAt(i);
            }
            var currentSet = new HashSet<PointSubValueVO>(target);
            foreach (var s in result)
            {
                if (!currentSet.Contains(s))
                    target.Add(s);
            }
            if (!target.SequenceEqual(result))
            {
                target.Clear();
                foreach (var s in result) target.Add(s);
            }
        }

        /// <summary>
        /// 将结果同步到展示集合：保留对象引用，仅做增删；顺序不一致时整体重建（对象引用不变）。
        /// </summary>
        private static void SyncCollection(ObservableCollection<PointValueVO> target, List<PointValueVO> result)
        {
            var resultSet = new HashSet<PointValueVO>(result);
            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!resultSet.Contains(target[i]))
                    target.RemoveAt(i);
            }
            var currentSet = new HashSet<PointValueVO>(target);
            foreach (var p in result)
            {
                if (!currentSet.Contains(p))
                    target.Add(p);
            }

            if (!target.SequenceEqual(result))
            {
                target.Clear();
                foreach (var p in result) target.Add(p);
            }
        }

        /// <summary>根据当前业务点位集合，动态维护区域筛选下拉中的业务类名选项（去重，增量更新不打断选择）。</summary>
        private void SyncAreaTypeOptions()
        {
            var businessTypes = _allPoints
                .Where(p => p.IsBusinessPoint)
                .Select(p => p.AreaType)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 固定前缀：全部 + 区域类型（本身无重复，业务类名可能与之同名时去重）
            var expected = new List<string> { "全部", "AI", "AO", "DI", "DO" };
            foreach (var t in businessTypes)
            {
                if (!expected.Contains(t, StringComparer.OrdinalIgnoreCase))
                    expected.Add(t);
            }

            // 无变化则跳过，避免每秒 Clear 重建打断下拉选择
            if (AreaTypeOptions.SequenceEqual(expected)) return;

            AreaTypeOptions.Clear();
            foreach (var e in expected) AreaTypeOptions.Add(e);

            // 仅当当前筛选值已不在选项中才回退到全部
            if (!string.IsNullOrEmpty(FilterAreaType) && !AreaTypeOptions.Contains(FilterAreaType))
                FilterAreaType = "全部";
        }

        /// <summary>
        /// 区域类型的固定排序权重：AI/AO/DI/DO 依次在前，未知区域按字母序兜底。
        /// </summary>
        private static int GetAreaTypeOrder(string? areaType)
        {
            var known = new[] { "AI", "AO", "DI", "DO" };
            if (string.IsNullOrEmpty(areaType)) return known.Length;
            var idx = Array.FindIndex(known, a => a.Equals(areaType, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? idx : known.Length;
        }

        /// <summary>
        /// 地址排序键：优先按数值（进制随设备代号区分），解析失败回退到字符串比较。
        /// </summary>
        private static (int Rank, long Addr, string Text) GetAddressSortKey(PointValueVO p)
        {
            var numberBase = PlcAddressHelper.IsHexDevice(p.DeviceCode) ? 16 : 10;
            if (PlcAddressHelper.TryParseAddress(p.PlcAddress, numberBase, out long addr))
                return (0, addr, string.Empty);
            return (1, 0, p.PlcAddress ?? string.Empty);
        }

        /// <summary>
        /// 清空所有筛选条件。
        /// </summary>
        [RelayCommand]
        private void ClearFilter()
        {
            FilterSettingName = string.Empty;
            FilterAreaType = "全部";
            FilterDeviceCode = string.Empty;
            FilterPlcAddress = string.Empty;
            FilterSubAddress = string.Empty;
        }

        /// <summary>
        /// 写入子值到 PLC：用户在右侧明细表格编辑某个子值后提交。
        /// 定位当前选中单头（SelectedPoint）确定目标通讯设定，按子值的设备代号+地址写值。
        /// </summary>
        [RelayCommand]
        private async Task WriteSubValueAsync(PointSubValueVO? sub)
        {
            if (sub == null) return;

            var settingId = ActiveSettingId;
            if (settingId == null)
            {
                _dialogService.ShowWarning("请先在左侧选择要写入的点位");
                return;
            }

            var deviceCode = sub.DeviceCode ?? string.Empty;
            var plcAddress = sub.PlcAddress ?? string.Empty;
            var text = sub.Value?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(plcAddress))
            {
                _dialogService.ShowWarning("设备代号或 PLC 地址为空，无法写入");
                return;
            }

            // 解析值类型：位设备写 bool，字设备写数值
            object value;
            try
            {
                if (PlcHelper.IsBitDevice(deviceCode))
                {
                    value = text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || text.Equals("on", StringComparison.OrdinalIgnoreCase);
                }
                else if (ushort.TryParse(text, out var us))
                {
                    value = us;
                }
                else if (int.TryParse(text, out var iv))
                {
                    value = iv;
                }
                else if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dv))
                {
                    value = dv;
                }
                else
                {
                    _dialogService.ShowWarning($"无法解析输入值「{text}」为有效数值");
                    return;
                }
            }
            catch
            {
                _dialogService.ShowWarning($"无法解析输入值「{text}」");
                return;
            }

            var ok = await _plcService.WriteSinglePointAsync(settingId.Value, deviceCode, plcAddress, value);
            if (!ok)
            {
                _dialogService.ShowError($"写入 {deviceCode}{plcAddress} 失败，请确认通讯连接正常");
                return;
            }

            // 不立即刷新：写入后由定时器周期性扫描刷新值，避免与轮询周期冲突
        }

        /// <summary>
        /// 根据当前选中点位与子值地址筛选，重建右侧子值明细展示集合。
        /// 子值对象引用与 SelectedPoint.Values 共享，定时器更新值由 INPC 自动驱动。
        /// </summary>
        private void ApplySubFilter()
        {
            var filter = FilterSubAddress?.Trim() ?? string.Empty;

            IEnumerable<PointSubValueVO> source = ActiveSubValueSource;

            if (!string.IsNullOrEmpty(filter))
            {
                source = source.Where(s =>
                    s.PlcAddress?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
            }

            var result = source.ToList();

            // 同步展示集合：保留对象引用，仅做增删；顺序变化时重建
            var resultSet = new HashSet<PointSubValueVO>(result);
            for (int i = SubValues.Count - 1; i >= 0; i--)
            {
                if (!resultSet.Contains(SubValues[i]))
                    SubValues.RemoveAt(i);
            }
            var currentSet = new HashSet<PointSubValueVO>(SubValues);
            foreach (var s in result)
            {
                if (!currentSet.Contains(s))
                    SubValues.Add(s);
            }
            if (!SubValues.SequenceEqual(result))
            {
                SubValues.Clear();
                foreach (var s in result) SubValues.Add(s);
            }
        }
    }
}
