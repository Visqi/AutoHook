namespace AutoHook.Services;

public sealed class NotificationMasterService : IPluginService {
    public NotificationMasterAPI.NotificationMasterApi Api { get; } = new(Svc.Interface);
}
