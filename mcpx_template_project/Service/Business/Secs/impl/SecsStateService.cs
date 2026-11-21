using mcpx_template_project.Domain.Enums;
using System.Collections.Concurrent;

namespace mcpx_template_project.Service.Business.Secs.impl
{
    /// <summary>
    /// 基于 <see cref="ConcurrentDictionary{TKey,TValue}"/> 的内存状态机实现。
    /// </summary>
    public sealed class SecsStateService : ISecsStateService
    {
        private sealed class InstanceState
        {
            public SecsControlState Control = SecsControlState.EquipmentOffline;
            public bool Communicating;
        }

        private readonly ConcurrentDictionary<long, InstanceState> _states = new();

        /// <inheritdoc/>
        public event Action<long, SecsControlState>? ControlStateChanged;

        private InstanceState GetOrCreate(long instanceId)
            => _states.GetOrAdd(instanceId, _ => new InstanceState());

        /// <summary>状态值变化时通知订阅者（线程安全，无变化不触发）。</summary>
        private void NotifyIfChanged(long instanceId, SecsControlState old, SecsControlState current)
        {
            if (old == current) return;
            ControlStateChanged?.Invoke(instanceId, current);
        }

        public SecsControlState GetControlState(long instanceId)
            => GetOrCreate(instanceId).Control;

        public bool TrySetControlState(long instanceId, SecsControlState target)
        {
            var st = GetOrCreate(instanceId);
            lock (st)
            {
                if (!IsTransitionAllowed(st.Control, target)) return false;
                var old = st.Control;
                st.Control = target;
                NotifyIfChanged(instanceId, old, target);
                return true;
            }
        }

        public bool RequestLocal(long instanceId)
        {
            var st = GetOrCreate(instanceId);
            lock (st)
            {
                // REMOTE → LOCAL：仅在线远程下允许；HostOffline 下本机也可进入本地
                if (st.Control is SecsControlState.OnlineRemote or SecsControlState.HostOffline)
                {
                    var old = st.Control;
                    st.Control = SecsControlState.OnlineLocal;
                    NotifyIfChanged(instanceId, old, SecsControlState.OnlineLocal);
                    return true;
                }
                return false;
            }
        }

        public bool RequestRemote(long instanceId)
        {
            var st = GetOrCreate(instanceId);
            lock (st)
            {
                // LOCAL → REMOTE：仅在线本地下允许
                if (st.Control == SecsControlState.OnlineLocal)
                {
                    st.Control = SecsControlState.OnlineRemote;
                    Serilog.Log.Information("[SECS状态机] 实例 {InstanceId} 控制状态 OnlineLocal → OnlineRemote", instanceId);
                    NotifyIfChanged(instanceId, SecsControlState.OnlineLocal, SecsControlState.OnlineRemote);
                    return true;
                }
                Serilog.Log.Warning("[SECS状态机] 实例 {InstanceId} RequestRemote 失败：当前状态 {State}（需 OnlineLocal）", instanceId, st.Control);
                return false;
            }
        }

        public bool RequestOffline(long instanceId)
        {
            var st = GetOrCreate(instanceId);
            lock (st)
            {
                if (st.Control is SecsControlState.OnlineLocal or SecsControlState.OnlineRemote)
                {
                    var old = st.Control;
                    st.Control = SecsControlState.EquipmentOffline;
                    NotifyIfChanged(instanceId, old, SecsControlState.EquipmentOffline);
                    return true;
                }
                // 已是离线态，视为幂等成功
                return st.Control == SecsControlState.EquipmentOffline;
            }
        }

        public bool RequestOnline(long instanceId)
        {
            var st = GetOrCreate(instanceId);
            lock (st)
            {
                if (st.Control is SecsControlState.EquipmentOffline or SecsControlState.HostOffline)
                {
                    var old = st.Control;
                    st.Control = SecsControlState.OnlineLocal;
                    NotifyIfChanged(instanceId, old, SecsControlState.OnlineLocal);
                    return true;
                }
                return st.Control is SecsControlState.OnlineLocal or SecsControlState.OnlineRemote;
            }
        }

        public void MarkCommunicating(long instanceId) => GetOrCreate(instanceId).Communicating = true;

        public void MarkNotCommunicating(long instanceId) => GetOrCreate(instanceId).Communicating = false;

        public bool IsCommunicating(long instanceId) => GetOrCreate(instanceId).Communicating;

        /// <summary>控制状态机允许的转换表。</summary>
        private static bool IsTransitionAllowed(SecsControlState from, SecsControlState to)
        {
            return (from, to) switch
            {
                // 在线本地 ⇄ 在线远程
                (SecsControlState.OnlineLocal, SecsControlState.OnlineRemote) => true,
                (SecsControlState.OnlineRemote, SecsControlState.OnlineLocal) => true,

                // 任意在线态 → 设备离线
                (SecsControlState.OnlineLocal or SecsControlState.OnlineRemote, SecsControlState.EquipmentOffline) => true,

                // 设备离线 → 尝试在线 → Host 离线
                (SecsControlState.EquipmentOffline, SecsControlState.AttemptOnline) => true,
                (SecsControlState.AttemptOnline, SecsControlState.HostOffline) => true,
                (SecsControlState.AttemptOnline, SecsControlState.OnlineLocal) => true,

                // 设备离线 → 在线本地（Host 主动请求在线，直接进入本地）
                (SecsControlState.EquipmentOffline, SecsControlState.OnlineLocal) => true,

                // 相同状态幂等
                _ when from == to => true,

                _ => false,
            };
        }
    }
}
