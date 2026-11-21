using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ScottPlot;
using SkiaSharp;
using mcpx_template_project.Service.Admin;
using mcpx_template_project.Service.Business.Machine;
using mcpx_template_project.Service.Business.Plc;
using MachineEntity = mcpx_template_project.Domain.Entities.Business.Machine.Machine;
using MachinePlcEntity = mcpx_template_project.Domain.Entities.Business.Machine.MachinePlc;
using RecipeEntity = mcpx_template_project.Domain.Entities.Business.Recipe.Recipe;

namespace mcpx_template_project.ViewModels.Business.Dashboard
{
    /// <summary>
    /// 生产看板视图模型：KPI 指标卡 + 三块可视化。
    ///   1. 近 7 日产量趋势（柱形图，ScottPlot，示例数据）；
    ///   2. 各机台批量生产进度（横向进度条，数据来自 PLC 缓存：当前批量数 / 目标批量数）；
    ///   3. 配方读点位实时趋势（折线图，ScottPlot，温度+压力+速度全量读点，下拉切换机台）。
    /// 数据获取方式与首页一致：仅读通讯缓存，不主动请求 PLC，DispatcherTimer 周期刷新。
    /// </summary>
    public partial class PageDashboardViewModel : ObservableObject
    {
        private readonly IMachineService _machineService;
        private readonly IPlcObjectService _plcObjectService;
        private readonly IPlcCommunicationService _plcService;
        private readonly IDialogService _dialogService;
        private readonly DispatcherTimer _timer;

        /// <summary>机台列表缓存（避免每秒查库）</summary>
        private List<MachineEntity> _machines = new();

        // ---------- KPI 指标 ----------

        [ObservableProperty] private string _statusText = "正在加载机台...";
        [ObservableProperty] private string _todayProduction = "0";
        [ObservableProperty] private string _totalProduction = "0";
        [ObservableProperty] private string _runningCount = "0 / 0";
        [ObservableProperty] private string _avgEffectivePower = "0 kW";

        // ---------- 机台下拉 ----------

        public ObservableCollection<MachineOption> MachineOptions { get; } = new();

        [ObservableProperty] private MachineOption? _selectedMachine;

        // ---------- 批量生产进度 ----------

        public ObservableCollection<MachineProgressItem> MachineProgressItems { get; } = new();

        // ---------- ScottPlot 图表（由 code-behind 注入引用） ----------

        /// <summary>近 7 日产量趋势柱形图控件</summary>
        public ScottPlot.WPF.WpfPlot? TrendPlot { get; set; }

        /// <summary>配方读点位实时趋势折线图控件</summary>
        public ScottPlot.WPF.WpfPlot? RecipeTrendPlot { get; set; }

        // ---------- 配方读点位元数据（温度 + 压力 + 速度全量） ----------

        /// <summary>配方数值型读点位：名称 + 取值委托 + 单位。用于实时趋势图。</summary>
        private sealed record RecipePoint(string Name, Func<RecipeEntity, double> Getter, string Unit = "");

        private static readonly RecipePoint[] RecipePoints =
        {
            new("一段上热压轮温度", r => r.UpperLamiTmp_Stage1, "℃"),
            new("一段下热压轮温度", r => r.LowerLamiTmp_Stage1, "℃"),
            new("二段上热压轮温度", r => r.UpperLamiTmp_Stage2, "℃"),
            new("二段下热压轮温度", r => r.LowerLamiTmp_Stage2, "℃"),
            new("出板温度", r => r.PanelOutTmp, "℃"),
            new("上贴膜温度", r => r.UpperTackingTmp, "℃"),
            new("下贴膜温度", r => r.LowerTackingTmp, "℃"),
            new("输送速度", r => r.DrvSpeed, ""),
            new("上贴膜压力", r => r.UpperTackingPressure, ""),
            new("前留铜", r => r.FrontCuPos, ""),
            new("后留铜", r => r.RearCuPos, ""),
            new("一段压轮压力", r => r.RollerPressure_Stage1, ""),
            new("二段压轮压力", r => r.RollerPressure_Stage2, ""),
            new("贴膜时间", r => r.DurationFilm, "s"),
            new("板厚", r => r.PlateThickness, "mm"),
        };

        /// <summary>趋势图滚动窗口容量（个采样点，2s 一个 ≈ 10 分钟）</summary>
        private const int TrendCapacity = 300;

        /// <summary>每机台趋势数据缓冲（X 为采样序号，Y 为各点位值），仅当前选中机台累积。</summary>
        private readonly Dictionary<long, TrendBuffer> _trendBuffers = new();

        private sealed class TrendBuffer
        {
            public int Seq;
            public readonly List<double> Xs = new();
            public readonly List<List<double>> Ys;

            public TrendBuffer()
            {
                Ys = new List<List<double>>(RecipePoints.Length);
                for (var i = 0; i < RecipePoints.Length; i++)
                    Ys.Add(new List<double>(TrendCapacity));
            }
        }

        // ---------- 颜色 ----------

        /// <summary>趋势图各点位曲线颜色（ScottPlot 调色板循环）</summary>
        private static readonly ScottPlot.Color[] TrendColors = BuildTrendColors();

        /// <summary>系统真实可用的中文字体族名（SkiaSharp 匹配中文字符解析，避免字体名不匹配导致乱码）。</summary>
        private static readonly string ChineseFontName = ResolveChineseFontName();

        private static string ResolveChineseFontName()
        {
            try
            {
                using var typeface = SKFontManager.Default.MatchCharacter('汉');
                if (!string.IsNullOrWhiteSpace(typeface.FamilyName))
                    return typeface.FamilyName;
            }
            catch
            {
                // 忽略，回退到硬编码字体名
            }

            foreach (var candidate in new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun" })
            {
                try
                {
                    using var tf = SKFontManager.Default.MatchFamily(candidate);
                    if (!string.IsNullOrWhiteSpace(tf.FamilyName))
                        return tf.FamilyName;
                }
                catch
                {
                    // 继续尝试下一个
                }
            }

            return "Microsoft YaHei";
        }

        private static ScottPlot.Color[] BuildTrendColors()
        {
            var colors = new System.Collections.Generic.List<ScottPlot.Color>(RecipePoints.Length);
            var pal10 = new ScottPlot.Palettes.Category10();
            var pal20 = new ScottPlot.Palettes.Category20();
            for (var i = 0; i < RecipePoints.Length; i++)
            {
                if (i < 10) colors.Add(pal10.GetColor(i));
                else colors.Add(pal20.GetColor(i));
            }
            return colors.ToArray();
        }

        public PageDashboardViewModel(
            IMachineService machineService,
            IPlcObjectService plcObjectService,
            IPlcCommunicationService plcService,
            IDialogService dialogService)
        {
            _machineService = machineService;
            _plcObjectService = plcObjectService;
            _plcService = plcService;
            _dialogService = dialogService;

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += (_, _) => RefreshRealtime();
        }

        /// <summary>初始化：加载机台 + 构建柱形图，并启动实时刷新。</summary>
        public async Task InitializeAsync()
        {
            try
            {
                _machines = await _machineService.QueryAllAsync();

                MachineOptions.Clear();
                foreach (var m in _machines)
                {
                    var name = string.IsNullOrWhiteSpace(m.Name) ? $"机台 #{m.Id}" : m.Name;
                    MachineOptions.Add(new MachineOption(m.Id, m.CommSettingId, name));
                }

                // 默认选中第一台已连接机台
                SelectedMachine = MachineOptions.FirstOrDefault(o => o.CommSettingId > 0)
                    ?? MachineOptions.FirstOrDefault();

                StatusText = $"共 {_machines.Count} 台机台 · 实时数据 2s 刷新";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("加载看板数据失败：" + ex.Message);
                StatusText = "加载失败";
            }

            BuildTrendChart();
            RefreshRealtime();
            _timer.Start();
        }

        /// <summary>构建「近 7 日产量趋势」柱形图（示例数据，历史产量表落地后替换）。</summary>
        private void BuildTrendChart()
        {
            if (TrendPlot == null) return;

            var daily = new double[] { 1200, 1350, 1100, 1580, 1420, 1680, 1530 };
            var target = new double[] { 1500, 1500, 1500, 1500, 1500, 1500, 1500 };
            var labels = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

            var plot = TrendPlot.Plot;
            plot.Clear();
            plot.Font.Set(ChineseFontName);

            var positions = Enumerable.Range(0, daily.Length).Select(i => (double)i).ToArray();

            // 柱：日产量
            var bars = plot.Add.Bars(positions, daily);
            bars.Color = ScottPlot.Color.FromHex("#5B8DEF");
            bars.LegendText = "日产量";
            foreach (var bar in bars.Bars)
                bar.Label = ((int)bar.Value).ToString();

            // 线：目标产量
            var line = plot.Add.Scatter(positions, target);
            line.Color = ScottPlot.Color.FromHex("#E57373");
            line.LineWidth = 2;
            line.MarkerSize = 0;
            line.LegendText = "目标产量";

            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(positions, labels);
            plot.Axes.Margins(bottom: 0);
            plot.Axes.SetLimitsX(-0.5, daily.Length - 0.5);
            plot.Axes.SetLimitsY(0, daily.Max() * 1.15);
            plot.ShowLegend(Alignment.UpperRight);
            TrendPlot.Refresh();
        }

        /// <summary>从 PLC 缓存刷新：KPI + 批量进度 + 配方点位趋势。</summary>
        private void RefreshRealtime()
        {
            if (_machines.Count == 0) return;

            var stateNames = new[] { "停机", "待机", "自动运转", "未知" };
            var stateCount = new double[4];

            double todaySum = 0, totalSum = 0, powerSum = 0;
            var connectedMachines = 0;
            var running = 0;

            // 批量进度临时集合（每轮重建，保证顺序与机台列表一致）
            var progressItems = new List<MachineProgressItem>();

            // 当前选中机台的配方数据（用于趋势图）
            RecipeEntity? selectedRecipe = null;

            foreach (var machine in _machines)
            {
                var settingId = machine.CommSettingId;
                if (settingId <= 0 || !_plcService.IsConnected(settingId)) continue;

                var plc = _plcObjectService.Read<MachinePlcEntity>(settingId);
                if (plc == null) continue;

                connectedMachines++;

                // PLC 状态值可能越界，越界归入「未知」
                var stateValue = (int)plc.MachineState;
                stateCount[stateValue is >= 0 and <= 2 ? stateValue : 3]++;
                if (stateValue == 2) running++;

                var daily = (double)plc.DailyProduction;
                var total = (double)plc.TotalProduction;
                var power = (double)plc.EffectivePower;

                todaySum += daily;
                totalSum += total;
                powerSum += power;

                // 批量进度：当前批量数 / 目标批量数
                var cur = plc.CurrentBatchCount;
                var target = plc.TargetBatchCount;
                var name = string.IsNullOrWhiteSpace(machine.Name) ? $"机台 #{machine.Id}" : machine.Name;
                progressItems.Add(new MachineProgressItem(name, cur, target));

                // 若为当前选中机台，读取配方缓存
                if (SelectedMachine != null && machine.Id == SelectedMachine.MachineId)
                    selectedRecipe = _plcObjectService.Read<RecipeEntity>(settingId);
            }

            // KPI
            TodayProduction = todaySum.ToString("N0");
            TotalProduction = totalSum.ToString("N0");
            RunningCount = $"{running} / {_machines.Count}";
            AvgEffectivePower = connectedMachines == 0
                ? "0 kW"
                : (powerSum / connectedMachines).ToString("F1") + " kW";

            // 批量进度（重建集合，保证与机台列表同步）
            MachineProgressItems.Clear();
            foreach (var item in progressItems)
                MachineProgressItems.Add(item);

            // 配方点位趋势
            AppendTrendSample(selectedRecipe);
        }

        /// <summary>向趋势缓冲追加一个采样点，并重绘当前选中机台的趋势图。</summary>
        private void AppendTrendSample(RecipeEntity? recipe)
        {
            if (RecipeTrendPlot == null || SelectedMachine == null) return;

            var machineId = SelectedMachine.MachineId;

            if (!_trendBuffers.TryGetValue(machineId, out var buffer))
            {
                buffer = new TrendBuffer();
                _trendBuffers[machineId] = buffer;
            }

            // 追加采样序号
            buffer.Seq++;
            buffer.Xs.Add(buffer.Seq);

            // 追加各点位当前值（无缓存时以 0 占位，保持曲线长度一致）
            for (var i = 0; i < RecipePoints.Length; i++)
            {
                var value = recipe != null ? RecipePoints[i].Getter(recipe) : 0.0;
                buffer.Ys[i].Add(value);
            }

            // 滚动窗口截断
            if (buffer.Xs.Count > TrendCapacity)
            {
                var remove = buffer.Xs.Count - TrendCapacity;
                buffer.Xs.RemoveRange(0, remove);
                foreach (var ys in buffer.Ys)
                    ys.RemoveRange(0, remove);
            }

            DrawTrendChart(buffer);
        }

        /// <summary>重绘配方点位实时趋势折线图。</summary>
        private void DrawTrendChart(TrendBuffer buffer)
        {
            if (RecipeTrendPlot == null) return;

            var plot = RecipeTrendPlot.Plot;
            plot.Clear();
            plot.Font.Set(ChineseFontName);

            var xs = buffer.Xs.ToArray();

            for (var i = 0; i < RecipePoints.Length; i++)
            {
                var ys = buffer.Ys[i].ToArray();
                if (ys.Length == 0) continue;

                var line = plot.Add.Scatter(xs, ys);
                line.Color = TrendColors[i % TrendColors.Length];
                line.LineWidth = 1.5f;
                line.MarkerSize = 0;
                line.LegendText = RecipePoints[i].Name;
            }

            plot.Axes.Bottom.Label.Text = "采样点（2s/点）";
            plot.Axes.Left.Label.Text = "数值";
            plot.Axes.Margins(0, 0.05);
            plot.ShowLegend(Alignment.UpperLeft);
            RecipeTrendPlot.Refresh();
        }

        /// <summary>机台切换时立即重绘该机台的趋势（无新采样也显示历史缓冲）。</summary>
        partial void OnSelectedMachineChanged(MachineOption? value)
        {
            if (value == null || RecipeTrendPlot == null) return;

            if (_trendBuffers.TryGetValue(value.MachineId, out var buffer))
                DrawTrendChart(buffer);
            else
            {
                var plot = RecipeTrendPlot.Plot;
                plot.Clear();
                plot.Font.Set(ChineseFontName);
                plot.Axes.Bottom.Label.Text = "采样点（2s/点）";
                plot.Axes.Left.Label.Text = "数值";
                RecipeTrendPlot.Refresh();
            }
        }
    }

    /// <summary>机台下拉选项。</summary>
    public sealed class MachineOption
    {
        public MachineOption(long machineId, long commSettingId, string name)
        {
            MachineId = machineId;
            CommSettingId = commSettingId;
            DisplayName = name;
        }

        public long MachineId { get; }
        public long CommSettingId { get; }
        public string DisplayName { get; }
    }

    /// <summary>批量生产进度条目（当前批量数 / 目标批量数）。</summary>
    public sealed partial class MachineProgressItem : ObservableObject
    {
        public MachineProgressItem(string name, int current, int target)
        {
            Name = name;
            Current = current;
            Target = target;
        }

        public string Name { get; }

        public int Current { get; }

        public int Target { get; }

        /// <summary>百分比文案，如「1,234 / 1,500（82%）」</summary>
        public string PercentText
        {
            get
            {
                if (Target <= 0) return $"{Current:N0} / —";
                var pct = Current * 100.0 / Target;
                return $"{Current:N0} / {Target:N0}（{pct:F0}%）";
            }
        }

        /// <summary>进度条填充宽度（相对值 0~1，绑定时乘以容器宽度）。</summary>
        public double ProgressWidth => Target <= 0 ? 0 : Math.Clamp(Current / (double)Target, 0, 1);

        /// <summary>进度条颜色（完成率越高越偏绿，越低越偏橙）。</summary>
        public System.Windows.Media.Brush BarBrush
        {
            get
            {
                if (Target <= 0) return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158));
                var pct = Current / (double)Target;
                var brush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(
                        (byte)(255 - 200 * pct),
                        (byte)(140 + 80 * pct),
                        56));
                brush.Freeze();
                return brush;
            }
        }
    }
}
