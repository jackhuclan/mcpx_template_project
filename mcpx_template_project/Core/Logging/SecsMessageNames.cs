using System.Collections.Generic;

namespace mcpx_template_project.Core.Logging
{
    /// <summary>
    /// SECS 消息名映射表（S/F → 名称）。
    /// secs4net 收到的消息 Name 为 null，本表用于给收发报文补全语义名；
    /// 主动构造且已设置 Name 的消息不受影响。
    /// </summary>
    public static class SecsMessageNames
    {
        private static readonly IReadOnlyDictionary<(byte S, byte F), string> Names = Build();

        /// <summary>按 Stream/Function 取消息名，未命中返回 null。</summary>
        public static string? Get(byte s, byte f) =>
            Names.TryGetValue((s, f), out var name) ? name : null;

        private static IReadOnlyDictionary<(byte, byte), string> Build()
        {
            var d = new Dictionary<(byte, byte), string>();

            // Stream 1 设备状态
            d[(1, 1)] = "AreYouThereRequest";
            d[(1, 2)] = "OnLineData";
            d[(1, 3)] = "SelectedEquipmentStatusRequest";
            d[(1, 4)] = "SelectedEquipmentStatusData";
            d[(1, 11)] = "StatusVariableNameListRequest";
            d[(1, 12)] = "StatusVariableNameListReply";
            d[(1, 13)] = "EstablishCommunicationsRequest";
            d[(1, 14)] = "EstablishCommunicationsAcknowledge";
            d[(1, 15)] = "RequestOffLine";
            d[(1, 16)] = "OffLineAcknowledge";
            d[(1, 17)] = "RequestOnLine";
            d[(1, 18)] = "OnLineAcknowledge";

            // Stream 2 设备控制与诊断
            d[(2, 13)] = "EquipmentConstantRequest";
            d[(2, 14)] = "EquipmentConstantData";
            d[(2, 15)] = "NewEquipmentConstantSend";
            d[(2, 16)] = "NewEquipmentConstantAcknowledge";
            d[(2, 17)] = "DateAndTimeRequest";
            d[(2, 18)] = "DateAndTimeData";
            d[(2, 23)] = "TraceInitializeSend";
            d[(2, 24)] = "TraceInitializeAcknowledge";
            d[(2, 25)] = "LoopBackDiagnosisRequest";
            d[(2, 26)] = "LoopBackDiagnosisData";
            d[(2, 29)] = "EquipmentConstantNameListRequest";
            d[(2, 30)] = "EquipmentConstantNameList";
            d[(2, 31)] = "DateAndTimeSend";
            d[(2, 32)] = "DateAndTimeAcknowledge";
            d[(2, 33)] = "DefineReport";
            d[(2, 34)] = "DefineReportAcknowledge";
            d[(2, 35)] = "LinkEventReport";
            d[(2, 36)] = "LinkEventReportAcknowledge";
            d[(2, 37)] = "EnableDisableEventReport";
            d[(2, 38)] = "EnableDisableEventReportAcknowledge";
            d[(2, 39)] = "MultiBlockInquire";
            d[(2, 40)] = "MultiBlockGrant";
            d[(2, 41)] = "RemoteCommandSend";
            d[(2, 42)] = "RemoteCommandAcknowledge";
            d[(2, 43)] = "ResetSpooling";
            d[(2, 44)] = "ResetSpoolingAcknowledge";
            d[(2, 45)] = "DefineVariableLimitAttributes";
            d[(2, 46)] = "VariableLimitAttributesAcknowledge";
            d[(2, 47)] = "VariableLimitAttributesRequest";
            d[(2, 48)] = "VariableLimitAttributesSend";
            d[(2, 49)] = "EnhancedRemoteCommand";
            d[(2, 50)] = "EnhancedRemoteCommandAcknowledge";

            // Stream 5 报警
            d[(5, 1)] = "AlarmReportSend";
            d[(5, 2)] = "AlarmReportAcknowledge";
            d[(5, 3)] = "EnableDisableAlarmSend";
            d[(5, 4)] = "EnableDisableAlarmAcknowledge";
            d[(5, 5)] = "ListAlarmRequest";
            d[(5, 6)] = "ListAlarmData";
            d[(5, 7)] = "ListEnabledAlarmRequest";
            d[(5, 8)] = "ListEnabledAlarmData";

            // Stream 6 数据采集
            d[(6, 1)] = "TraceDataSend";
            d[(6, 2)] = "TraceDataAcknowledge";
            d[(6, 5)] = "MultiBlockDataSendInquire";
            d[(6, 6)] = "MultiBlockGrant";
            d[(6, 11)] = "EventReportSend";
            d[(6, 12)] = "EventReportAcknowledge";
            d[(6, 15)] = "EventReportRequest";
            d[(6, 16)] = "EventReportData";
            d[(6, 19)] = "IndividualReportRequest";
            d[(6, 20)] = "IndividualReportData";
            d[(6, 23)] = "RequestSpooledData";
            d[(6, 24)] = "RequestSpooledDataAcknowledgementSend";

            // Stream 7 工艺程序
            d[(7, 1)] = "ProcessProgramLoadInquire";
            d[(7, 2)] = "ProcessProgramLoadGrant";
            d[(7, 3)] = "ProcessProgramSend";
            d[(7, 4)] = "ProcessProgramAcknowledge";
            d[(7, 5)] = "ProcessProgramRequest";
            d[(7, 6)] = "ProcessProgramData";
            d[(7, 17)] = "DeleteProcessProgramSend";
            d[(7, 18)] = "DeleteProcessProgramAcknowledge";
            d[(7, 19)] = "CurrentEPPDRequest";
            d[(7, 20)] = "CurrentEPPDData";
            d[(7, 23)] = "FormattedProcessProgramSend";
            d[(7, 24)] = "FormattedProcessProgramAcknowledge";
            d[(7, 25)] = "FormattedProcessProgramRequest";
            d[(7, 26)] = "FormattedProcessProgramData";

            // Stream 9 系统错误
            d[(9, 1)] = "UnrecognizedDeviceId";
            d[(9, 3)] = "UnrecognizedStreamType";
            d[(9, 5)] = "UnrecognizedFunctionType";
            d[(9, 7)] = "IllegalData";
            d[(9, 9)] = "TransactionTimerTimeout";
            d[(9, 11)] = "DataTooLong";
            d[(9, 13)] = "ConversationTimeout";

            // Stream 10 终端服务
            d[(10, 1)] = "TerminalRequest";
            d[(10, 2)] = "TerminalRequestAcknowledge";
            d[(10, 3)] = "TerminalDisplaySingle";
            d[(10, 4)] = "TerminalDisplaySingleAcknowledge";
            d[(10, 5)] = "TerminalDisplayMultiBlock";
            d[(10, 6)] = "TerminalDisplayMultiBlockAcknowledge";

            return d;
        }
    }
}
