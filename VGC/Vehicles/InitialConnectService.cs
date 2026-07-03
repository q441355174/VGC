namespace VGC.Vehicles;

public enum InitialConnectState
{
    Disconnected,
    WaitingForHeartbeat,
    RequestingParameters,
    RequestingMission,
    RequestingHomePosition,
    RequestingComponentInformation,
    Ready,
    Failed
}

public sealed record InitialConnectSnapshot(
    InitialConnectState State,
    double Progress,
    string StatusText,
    string? Error = null);

public sealed class InitialConnectService
{
    private InitialConnectState _state = InitialConnectState.Disconnected;
    private string? _error;
    private int _parametersReceived;
    private int _parametersExpected;
    private int _componentInformationReceived;
    private int _componentInformationExpected;

    public InitialConnectState State => _state;

    public InitialConnectSnapshot Snapshot => new(
        _state,
        CalculateProgress(),
        GetStatusText(),
        _error);

    public void MarkHeartbeatReceived()
    {
        if (_state == InitialConnectState.Disconnected)
        {
            _state = InitialConnectState.WaitingForHeartbeat;
        }
    }

    public void BeginParameterRequest(int expectedCount)
    {
        if (_state is InitialConnectState.WaitingForHeartbeat or InitialConnectState.Ready)
        {
            _state = InitialConnectState.RequestingParameters;
            _parametersExpected = expectedCount;
            _parametersReceived = 0;
        }
    }

    public void MarkParameterReceived()
    {
        _parametersReceived++;
        if (_parametersExpected > 0 && _parametersReceived >= _parametersExpected)
        {
            _state = InitialConnectState.RequestingMission;
        }
    }

    public void MarkMissionReceived()
    {
        _state = InitialConnectState.RequestingHomePosition;
    }

    public void MarkHomeReceived()
    {
        _state = InitialConnectState.RequestingComponentInformation;
    }

    public void BeginComponentInformationRequest(int expectedCount)
    {
        if (_state is InitialConnectState.RequestingComponentInformation or InitialConnectState.Ready)
        {
            _state = InitialConnectState.RequestingComponentInformation;
            _componentInformationExpected = Math.Max(0, expectedCount);
            _componentInformationReceived = 0;
        }
    }

    public void MarkComponentInformationReceived()
    {
        _componentInformationReceived++;
        if (_componentInformationExpected == 0 || _componentInformationReceived >= _componentInformationExpected)
        {
            _state = InitialConnectState.Ready;
        }
    }

    public void MarkFailed(string error)
    {
        _state = InitialConnectState.Failed;
        _error = error;
    }

    public void Reset()
    {
        _state = InitialConnectState.Disconnected;
        _error = null;
        _parametersReceived = 0;
        _parametersExpected = 0;
        _componentInformationReceived = 0;
        _componentInformationExpected = 0;
    }

    private double CalculateProgress()
    {
        return _state switch
        {
            InitialConnectState.Disconnected => 0,
            InitialConnectState.WaitingForHeartbeat => 0.1,
            InitialConnectState.RequestingParameters => 0.2 + (_parametersExpected > 0 ? 0.3 * _parametersReceived / _parametersExpected : 0),
            InitialConnectState.RequestingMission => 0.5,
            InitialConnectState.RequestingHomePosition => 0.8,
            InitialConnectState.RequestingComponentInformation => 0.9,
            InitialConnectState.Ready => 1.0,
            InitialConnectState.Failed => 0,
            _ => 0
        };
    }

    private string GetStatusText()
    {
        return _state switch
        {
            InitialConnectState.Disconnected => "Disconnected",
            InitialConnectState.WaitingForHeartbeat => "Waiting for heartbeat",
            InitialConnectState.RequestingParameters => $"Loading parameters ({_parametersReceived}/{_parametersExpected})",
            InitialConnectState.RequestingMission => "Loading mission",
            InitialConnectState.RequestingHomePosition => "Loading home position",
            InitialConnectState.RequestingComponentInformation => _componentInformationExpected > 0
                ? $"Loading component information ({_componentInformationReceived}/{_componentInformationExpected})"
                : "Loading component information",
            InitialConnectState.Ready => "Ready",
            InitialConnectState.Failed => $"Failed: {_error}",
            _ => "Unknown"
        };
    }
}
