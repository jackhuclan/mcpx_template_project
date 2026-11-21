using mcpx_template_project.Core.Logging;
using mcpx_template_project.Service.Business.Plc;

namespace mcpx_template_project.Service.Business.Machine.impl
{
    /// <summary>
    /// PLC 统一后台刷新服务：连接建立后的周期刷新入口。
    ///
    /// 架构约定：<b>读 PLC 只有一个入口</b>，即
    /// <see cref="IPlcCommunicationService.ReadAllPointsAsync"/>（批量读取某通讯设定下的全部点位，
    /// 含区域配置点位与业务注解点位——机台 MachinePlc、配方 Recipe 等）。
    /// 本服务只做「周期性调用该唯一读入口刷新缓存」，绝不逐点直读 PLC。
    ///
    /// 首页、配方页、实时监控页的 ViewModel 都只通过
    /// <see cref="IPlcCommunicationService.GetCachedPoints"/>（或 PlcObjectService.Read&lt;T&gt;）从缓存读值，
    /// 绝不主动向 PLC 发请求。实时监控页仅用于调试观察与写入点位，不承担刷新职责。
    /// </summary>
    public class PlcPollService : IDisposable
    {
        private readonly IPlcCommunicationService _plcService;
        private readonly ILogService? _logService;

        private readonly object _gate = new();
        private System.Threading.Timer? _timer;
        private bool _running;
        private bool _polling;

        /// <summary>轮询周期（毫秒）</summary>
        private const int PollIntervalMs = 1000;

        public PlcPollService(
            IPlcCommunicationService plcService,
            ILogService? logService = null)
        {
            _plcService = plcService;
            _logService = logService;
        }

        /// <summary>启动后台刷新（幂等）。</summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_running) return;
                _running = true;

                _timer ??= new System.Threading.Timer(
                    _ => _ = PollOnceAsync(),
                    null,
                    PollIntervalMs,
                    PollIntervalMs);
            }
        }

        /// <summary>停止后台刷新。</summary>
        public void Stop()
        {
            lock (_gate)
            {
                _running = false;
                _timer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            }
        }

        /// <summary>立即执行一次全量刷新（唯一读入口：ReadAllPointsAsync）。</summary>
        public async Task PollOnceAsync()
        {
            lock (_gate)
            {
                if (_polling) return;
                _polling = true;
            }

            try
            {
                // 遍历所有已连接的通讯设定，统一批量读全部点位刷新缓存
                foreach (var settingId in _plcService.GetConnectedSettingIds())
                {
                    await _plcService.ReadAllPointsAsync(settingId);
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError(ex, "[PLC后台刷新] 周期刷新异常");
            }
            finally
            {
                lock (_gate)
                    _polling = false;
            }
        }

        public void Dispose()
        {
            Stop();
            _timer?.Dispose();
            _timer = null;
        }
    }
}
