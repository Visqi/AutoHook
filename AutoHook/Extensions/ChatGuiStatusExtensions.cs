namespace AutoHook.Extensions;

public static class ChatGuiStatusExtensions {
    extension(IChatGui chat) {
        public void PrintStatus(string message) {
            PluginUi.Status = message;
            if (!Configuration.C.ShowChatLogs)
                return;

            chat.EchoMessage(message);
            IPluginLog.Get().Info(message);
        }
    }
}
