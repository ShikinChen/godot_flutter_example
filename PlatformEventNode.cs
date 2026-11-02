using Godot;
using System;
using System.Text.Json;

// 常驻接收 AndroidEventPlugin 的消息，场景切换后仍保留连接。
public partial class PlatformEventNode : Node
{
    private const string PluginName = "PlatformEvent";
    private const string PlatformSignal = "platform_event";
    private GodotObject _plugin;
    private Callable _receiver;

    [Signal]
    public delegate void PlatformEventMsgEventHandler(string widgetId, string msg);

    public override void _Ready()
    {
        PlatformEventMsg += HandlePlatformEvent;
        if (!Engine.HasSingleton(PluginName))
            return;

        _plugin = Engine.GetSingleton(PluginName);
        _receiver = new Callable(this, nameof(OnPlatformEventMsg));
        if (!_plugin.HasSignal(PlatformSignal))
        {
            GD.PushWarning($"{PluginName} 未声明信号 {PlatformSignal}");
            return;
        }

        var error = _plugin.Connect(PlatformSignal, _receiver);
        if (error != Error.Ok)
            GD.PushError($"连接 Android 事件失败: {error}");
    }

    private void OnPlatformEventMsg(string msg)
    {
        // 延迟到主线程处理，避免在原生信号回调中修改场景树。
        Callable.From(() => DispatchMessage(msg)).CallDeferred();
    }

    private void DispatchMessage(string msg)
    {
        try
        {
            using var document = JsonDocument.Parse(msg);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("事件必须为 JSON 对象");

            var widgetId = "";
            if (root.TryGetProperty("msg", out var payload))
            {
                if (root.TryGetProperty("widgetId", out var id))
                    widgetId = id.GetString() ?? "";
                msg = payload.GetString() ?? throw new JsonException("msg 不能为空");
            }

            EmitSignal(SignalName.PlatformEventMsg, widgetId, msg);
        }
        catch (Exception exception)
        {
            GD.PushError($"Android 事件解析失败: {exception.Message}");
        }
    }

    private void HandlePlatformEvent(string widgetId, string msg)
    {
        GD.Print($"OnPlatformEventMsg[{widgetId}]: {msg}");
        using var document = JsonDocument.Parse(msg);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            GD.PushWarning("Android 事件缺少字符串 type");
            return;
        }

        switch (type.GetString()?.ToLowerInvariant())
        {
            case "toscene":
                if (!root.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Object ||
                    !data.TryGetProperty("sceneName", out var sceneName) ||
                    sceneName.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(sceneName.GetString()))
                {
                    GD.PushWarning("ToScene.data 缺少有效的 sceneName");
                    return;
                }

                var name = sceneName.GetString();
                var path = name.StartsWith("res://", StringComparison.Ordinal) ? name : $"res://{name}";
                if (!path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                    path += ".tscn";
                if (!ResourceLoader.Exists(path, "PackedScene"))
                {
                    GD.PushWarning($"场景不存在: {path}");
                    return;
                }

                var error = GetTree().ChangeSceneToFile(path);
                if (error != Error.Ok)
                    GD.PushError($"切换场景失败: {path}, {error}");
                break;
            default:
                GD.PushWarning($"未处理的 Android 事件: {type.GetString()}");
                break;
        }
    }

    public void SendPlatformEventMsg(string widgetId, string msg)
    {
        if (!GodotObject.IsInstanceValid(_plugin))
            return;

        var payload = string.IsNullOrEmpty(widgetId)
            ? msg
            : JsonSerializer.Serialize(new { widgetId, msg });
        _plugin.Call("onGodotEventMsg", payload);
    }

    public override void _ExitTree()
    {
        PlatformEventMsg -= HandlePlatformEvent;
        if (GodotObject.IsInstanceValid(_plugin) && _plugin.IsConnected(PlatformSignal, _receiver))
            _plugin.Disconnect(PlatformSignal, _receiver);
    }
}
