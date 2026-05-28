using Solace.EventBus.Client;
using Serilog;

namespace Solace.ApiServer.Utils;

public sealed class AdminRequestHandler : IRequestHandlerLister
{
    private readonly ShopManager _shopManager;

    public AdminRequestHandler(ShopManager shopManager)
    {
        _shopManager = shopManager;
    }

    public static async Task StartAsync(EventBusClient eventBus, ShopManager shopManager)
    {
        var handler = new AdminRequestHandler(shopManager);
        await eventBus.AddRequestHandlerAsync("api_admin", handler);
    }

    public async Task HandleRequestAsync(string message, RequestHandler handler)
    {
        if (message == "RELOAD_SHOP")
        {
            Log.Information("Received reload shop request via EventBus");
            await _shopManager.ReloadAsync();
            await handler.SendResponseAsync("OK");
        }
        else
        {
            await handler.SendResponseAsync("UNKNOWN_COMMAND");
        }
    }

    public async Task ErrorAsync()
    {
        Log.Error("AdminRequestHandler EventBus error");
    }
}
