using System.Linq;
using MarketMogul.Core.Universalis;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MarketMogul.Services;
using MarketMogul.Windows;

namespace MarketMogul;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IMarketBoard MarketBoardService { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/mogul";

    public Configuration Configuration { get; init; }
    public ItemCatalog Catalog { get; }
    public WorldTracker Worlds { get; }
    public ScanService Scanner { get; }
    public HomePriceService HomePrices { get; }
    public MarketBoardWatcher MarketBoard { get; }

    private readonly UniversalisClient universalis;

    public readonly WindowSystem WindowSystem = new("MarketMogul");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private MarketBoardWindow MarketBoardWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Catalog = new ItemCatalog(DataManager);
        Worlds = new WorldTracker(PlayerState, Framework);
        universalis = new UniversalisClient($"MarketMogul/{PluginInterface.Manifest.AssemblyVersion} (Dalamud plugin)");
        Scanner = new ScanService(universalis, Catalog, Log);
        HomePrices = new HomePriceService(universalis, Log);
        MarketBoard = new MarketBoardWatcher(MarketBoardService);
        Worlds.WorldChanged += OnWorldChanged;

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        MarketBoardWindow = new MarketBoardWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(MarketBoardWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Ouvre la fenêtre des opportunités d'achat-revente.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        Worlds.WorldChanged -= OnWorldChanged;
        Worlds.Dispose();
        Scanner.Dispose();
        MarketBoard.Dispose();
        universalis.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    /// <summary>À l'arrivée sur un autre monde, rappelle ce qu'il y a à y acheter.</summary>
    private void OnWorldChanged()
    {
        if (!Worlds.IsVisiting || Scanner.ScannedHome?.Id != Worlds.Home!.Id)
            return;

        var worldId = Worlds.Current!.Id;
        var here = Scanner.Opportunities.Where(o => o.Purchases.Any(p => p.WorldId == worldId)).ToList();
        if (here.Count == 0)
            return;

        var profit = here.Sum(o => o.ExpectedProfit);
        ChatGui.Print($"{here.Count} achat(s) recommandé(s) sur {Worlds.Current.Name}, profit attendu ~{profit:N0} gils. /mogul pour le détail.", "MarketMogul");
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
