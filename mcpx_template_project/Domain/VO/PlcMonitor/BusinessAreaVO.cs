using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace mcpx_template_project.Domain.VO
{
    /// <summary>
    /// 业务区域聚合行：左侧业务区列表中的一行，对应一个「通讯设定 × 业务类」组合。
    /// 例如 McpX1 + MachinePlc、McpX1 + Recipe、McpX2 + MachinePlc。
    /// 点击该行，右侧明细展示该类下所有属性点位（<see cref="Children"/>）。
    /// </summary>
    public partial class BusinessAreaVO : ObservableObject
    {
        /// <summary>所属通讯设定ID（用于写入 PLC）</summary>
        public long SettingId { get; set; }

        /// <summary>通讯设定名称</summary>
        public string SettingName { get; set; } = string.Empty;

        /// <summary>业务类名（区域，如 MachinePlc / Recipe）</summary>
        public string AreaType { get; set; } = string.Empty;

        /// <summary>数据类型描述（位/字，取该区域首个子点的类型）</summary>
        [ObservableProperty]
        private string _dataType = string.Empty;

        /// <summary>是否在线（任一子点在线即视为在线）</summary>
        [ObservableProperty]
        private bool _isOnline;

        /// <summary>错误信息（聚合首个子点的错误信息，非空视为异常）</summary>
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        /// <summary>最近一次读取时间</summary>
        [ObservableProperty]
        private DateTime _updateTime = DateTime.Now;

        /// <summary>该区域下所有属性点位（右侧明细数据源，含 设备代号/地址/属性名/值）</summary>
        public ObservableCollection<PointSubValueVO> Children { get; } = new();

        /// <summary>计算属性：状态文本</summary>
        public string StatusText => IsOnline ? "正常" : (!string.IsNullOrEmpty(ErrorMessage) ? "异常" : "离线");
    }
}
