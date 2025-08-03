using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SpamrollGiveaway.Windows;
using FFXIVSharedLibrary.Chat;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace SpamrollGiveaway;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/spamroll";
    private const string CommandStartName = "/spamstart";
    private const string CommandStopName = "/spamstop";
    private const string CommandConfigName = "/spamconfig";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SpamrollGiveaway");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    // Game state
    private bool isGameActive = false;
    private readonly Dictionary<string, RollEventArgs> currentRolls = new();
    private readonly List<RollEventArgs> winners = new();
    private CancellationTokenSource? gameCancellation;
    private readonly object lockObject = new object();

    // FFXIVSharedLibrary components
    private ChatMessageProcessor chatProcessor;
    private RollHandler rollHandler;
    private RollCollector rollCollector;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        // Initialize FFXIVSharedLibrary components
        chatProcessor = new ChatMessageProcessor();
        rollHandler = new RollHandler(Configuration.LocalPlayerName, Configuration.DebugMode);
        rollCollector = new RollCollector();

        rollHandler.RollDetected += OnRollDetected;
        rollCollector.NewRollAdded += OnNewRollAdded;

        chatProcessor.RegisterHandler(rollHandler);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Spamroll Giveaway main window"
        });

        CommandManager.AddHandler(CommandStartName, new CommandInfo(OnStartCommand)
        {
            HelpMessage = "Starts collecting rolls for Spamroll Giveaway"
        });

        CommandManager.AddHandler(CommandStopName, new CommandInfo(OnStopCommand)
        {
            HelpMessage = "Stops the current Spamroll Giveaway round"
        });

        CommandManager.AddHandler(CommandConfigName, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Opens the Spamroll Giveaway configuration window"
        });

        ChatGui.ChatMessage += OnChatMessage;
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Log.Information($"Spamroll Giveaway loaded successfully!");
    }

    public void Dispose()
    {
        gameCancellation?.Cancel();
        gameCancellation?.Dispose();

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandStartName);
        CommandManager.RemoveHandler(CommandStopName);
        CommandManager.RemoveHandler(CommandConfigName);

        ChatGui.ChatMessage -= OnChatMessage;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;

        rollHandler.RollDetected -= OnRollDetected;
        rollCollector.NewRollAdded -= OnNewRollAdded;
    }

    private void OnCommand(string command, string args)
    {
        if (args == "config")
            ToggleConfigUI();
        else
            ToggleMainUI();
    }

    private void OnStartCommand(string command, string args) => StartGame();
    private void OnStopCommand(string command, string args) => StopGame();
    private void OnConfigCommand(string command, string args) => ToggleConfigUI();

    private void OnChatMessage(Dalamud.Game.Text.XivChatType type, int timestamp, ref Dalamud.Game.Text.SeStringHandling.SeString sender, ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
    {
        if (!isGameActive) return;

        chatProcessor.ProcessMessage((int)type, timestamp, sender.TextValue, message.TextValue);
    }

    private void OnRollDetected(RollEventArgs rollArgs)
    {
        if (!isGameActive) return;

        lock (lockObject)
        {
            rollCollector.AddRoll(rollArgs);
        }
    }

    private void OnNewRollAdded(RollEventArgs rollArgs)
    {
        lock (lockObject)
        {
            if (!isGameActive || !Configuration.WinningNumbers.Contains(rollArgs.RollValue))
                return;

            winners.Add(rollArgs);
            ChatGui.Print($"[Spamroll] WINNER! {rollArgs.NormalizedPlayerName} rolled {rollArgs.RollValue}!");

            if (Configuration.AutoCloseAfterWin)
            {
                StopGame();
            }
        }
    }

    public void StartGame()
    {
        if (isGameActive)
        {
            ChatGui.PrintError("[Spamroll] A game is already in progress!");
            return;
        }

        if (Configuration.WinningNumbers.Count == 0)
        {
            ChatGui.PrintError("[Spamroll] No winning numbers configured! Use /spamconfig to set them.");
            return;
        }

        lock (lockObject)
        {
            gameCancellation?.Cancel();
            gameCancellation = new CancellationTokenSource();

            isGameActive = true;
            currentRolls.Clear();
            winners.Clear();
            rollCollector.ClearRolls();

            var winningNumbersText = string.Join(", ", Configuration.WinningNumbers.OrderBy(n => n));
            ChatGui.Print($"[Spamroll] Game started! Winning numbers: {winningNumbersText}");
            ChatGui.Print("[Spamroll] Players, type /random to participate!");

            if (Configuration.RollTimeout > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(Configuration.RollTimeout * 1000, gameCancellation.Token);
                        if (isGameActive && winners.Count == 0)
                        {
                            ChatGui.Print("[Spamroll] Time's up! No winners this round.");
                            StopGame();
                        }
                    }
                    catch (OperationCanceledException) { }
                });
            }
        }
    }

    public void StopGame()
    {
        if (!isGameActive)
        {
            ChatGui.PrintError("[Spamroll] No game is currently active.");
            return;
        }

        lock (lockObject)
        {
            gameCancellation?.Cancel();
            isGameActive = false;

            var rollCount = rollCollector.GetRollCount();
            var winnerCount = winners.Count;

            ChatGui.Print($"[Spamroll] Game stopped. {rollCount} total rolls, {winnerCount} winners.");
        }
    }

    public bool IsGameActive => isGameActive;
    public IReadOnlyDictionary<string, RollEventArgs> GetCurrentRolls() => rollCollector.GetAllRolls();
    public IReadOnlyList<RollEventArgs> GetWinners() => winners;

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
