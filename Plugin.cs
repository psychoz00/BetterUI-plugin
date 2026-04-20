using BetterUI.Profiles;
using BetterUI.Tracking;
using BetterUI.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace BetterUI;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/bcd";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public ProfileStore ProfileStore { get; }
    public CooldownTracker Tracker { get; }

    public readonly WindowSystem WindowSystem = new("BetterUI");

    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private readonly BarRenderer barRenderer;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ProfileStore = new ProfileStore(Configuration, DataManager);
        Tracker = new CooldownTracker(DataManager);

        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);
        barRenderer = new BarRenderer(this, Tracker, TextureProvider, ClientState);

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open BetterUI. Use \"/bcd config\" for settings, \"/bcd lock\" to toggle the overlay lock."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.Draw += barRenderer.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("BetterUI loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= barRenderer.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        configWindow.Dispose();
        barRenderer.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var arg = args.Trim().ToLowerInvariant();
        switch (arg)
        {
            case "config":
            case "settings":
                ToggleConfigUi();
                break;
            case "lock":
                Configuration.OverlayLocked = !Configuration.OverlayLocked;
                Configuration.Save();
                ChatGui.Print($"[BetterUI] Overlay {(Configuration.OverlayLocked ? "locked" : "unlocked")}.");
                break;
            case "show":
                Configuration.ShowOverlay = true;
                Configuration.Save();
                break;
            case "hide":
                Configuration.ShowOverlay = false;
                Configuration.Save();
                break;
            default:
                ToggleMainUi();
                break;
        }
    }

    public void ToggleMainUi() => mainWindow.Toggle();
    public void ToggleConfigUi() => configWindow.Toggle();

    public static IDalamudTextureWrap? TryGetIcon(uint iconId)
    {
        if (iconId == 0) return null;
        try
        {
            return TextureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out var shared)
                ? shared.GetWrapOrDefault()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
