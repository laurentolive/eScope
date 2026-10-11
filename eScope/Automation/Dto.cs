namespace eScope.Automation
{
    // Results of the automation tools, serialized in snake_case (AutomationJson.Options).
    // null = not available / not computable.

    public record WorkspaceInfo(
        string Version,
        bool Recording,
        string? CurrentTrace,
        IReadOnlyList<DeviceInfo> Devices,
        IReadOnlyList<TraceInfo> Traces);

    public record DeviceInfo(
        string Name,
        string Protocol,
        string Com,
        string SymbolType,
        string SymbolFile,
        bool Connected,
        bool SupportsReadMem);

    public record DeviceStatus(
        string Name,
        string Protocol,
        string Com,
        string SymbolType,
        string SymbolFile,
        bool SymbolsLoaded,
        bool Connected,
        bool SupportsReadMem,
        long FramesReceived,
        int ChannelsRegistered);

    public record SymbolInfo(
        string Device,
        string Expression,
        bool Resolved,
        string? Address,
        int SizeBits,
        int Shift,
        string? Type);

    public record TimeRange(double FromMs, double ToMs);

    public record TraceInfo(
        string Name,
        bool Current,
        int Channels,
        double? DurationMs = null,
        TimeRange? View = null,
        TimeRange? Selection = null);

    public record ChannelInfo(
        string Name,
        string? Device,
        string? Symbol,
        bool HasDevice,
        bool HasFunction,
        string? Script,
        string? Error,
        string? Address,
        int SizeBits,
        int Shift,
        string Display,
        string Unit,
        string Color,
        bool Visible,
        int Samples,
        double PeriodMs,
        double StartMs,
        double? EndMs,
        double YGain,
        double YOffset,
        double XOffset);

    public record LostInfo(double TimeMs, int Count, bool Timeout);

    public record ChannelData(
        string Channel,
        string Unit,
        double PeriodMs,
        IReadOnlyList<double> TMs,
        IReadOnlyList<double> Value,
        IReadOnlyList<ulong>? Raw,
        IReadOnlyList<LostInfo> Lost);

    public record ChannelValue(
        string Channel,
        double? TMs,
        double? Value,
        ulong? Raw);

    public record ChannelAnalysis(
        string Channel,
        string Unit,
        int Samples,
        double? FromMs,
        double? ToMs,
        double? DurationMs,
        double? Min,
        double? Max,
        double? PeakToPeak,
        double? Mean,
        double? Rms,
        double? StdDev,
        double? FrequencyHz,
        double? PeriodMs,
        double? DutyPercent,
        int RisingEdges,
        double? FftPeakFrequencyHz,
        double? FftPeakAmplitude,
        IReadOnlyList<double>? FftFrequenciesHz,
        IReadOnlyList<double>? FftAmplitudes);

    public record EdgeList(
        string Channel,
        double Threshold,
        double Hysteresis,
        IReadOnlyList<double> RisingMs,
        IReadOnlyList<double> FallingMs);

    public record MarkerInfo(string Name, double TimeMs, string Color, bool Visible);
    public record DeltaInfo(string Name, double FromMs, double ToMs, double WidthMs, string Color, bool Visible);
    public record AnnotationInfo(string Text, string Channel, double TimeMs, string Color, bool Visible);
    public record DataMarkerInfo(string Name, string Channel, double Value, string Color, bool Visible);

    public record AnnotationList(
        string Trace,
        TimeRange? Selection,
        IReadOnlyList<MarkerInfo> Markers,
        IReadOnlyList<DeltaInfo> Deltas,
        IReadOnlyList<AnnotationInfo> Annotations,
        IReadOnlyList<DataMarkerInfo> DataMarkers);

    public record NameResult(string Name);
    public record PathResult(string Path);
    public record HistoryResult(bool Done, string? Command);
    public record SamplesRemoved(int Removed);

    public record RecordingState(bool Recording, string? Trace, double? ElapsedMs, bool Acquiring);

    // new_* fields: since the recording was started through the API (null when started by the user)
    public record ChannelAcquisition(
        string Channel,
        int Samples,
        int? NewSamples,
        int LostSamples,
        int Timeouts);

    public record RecordingSummary(
        string Trace,
        bool Recording,
        double? DurationMs,
        bool StoppedByUser,
        IReadOnlyList<ChannelAcquisition> Channels,
        IReadOnlyList<string> Warnings);

    // a variable read on the target. Text: value computed by GDB (not an lvalue, or larger than 64 bits).
    // Error: set by read_variables for an expression that failed (the other fields are then null)
    public record VariableValue(
        string Device,
        string Expression,
        string? Address,
        int? SizeBits,
        string? Type,
        double? Value,
        ulong? Raw,
        string? Hex,
        string? Text,
        string? Error = null);

    public record FileResult(string Path, long Bytes, int? Rows = null);
    public record LoadedTrace(string Name, string Path, int Channels, IReadOnlyList<string> MissingDevices);
}
