using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SpamrollGiveaway.Windows;
using FFXIVSharedLibrary.Chat;
using FFXIVSharedLibrary.Player;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using ECommons;
using ECommons.Automation;

namespace SpamrollGiveaway;

public class Winner
{
    public string PlayerName { get; set; } = "";
    public int RollValue { get; set; }
    public DateTime WinTime { get; set; }
    public int RollOrder { get; set; }
    public bool IsDebugRoll { get; set; }
}

public class RollData
{
    public string PlayerName { get; set; } = "";
    public int RollValue { get; set; }
    public DateTime RollTime { get; set; }
    public bool IsDebugRoll { get; set; }
}

public sealed class Plugin : IDalamudPlugin
{
    // Windows API for playing system sounds
    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
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
    private bool isGamePaused = false;
    private readonly List<Winner> gameWinners = new();
    private readonly List<RollData> allRolls = new();
    private readonly Dictionary<string, int> rollOrder = new(); // For tiebreaking
    private readonly HashSet<string> participants = new();
    private int rollCounter = 0;
    private CancellationTokenSource? gameCancellation;
    private readonly object lockObject = new object();
    private GameStats? currentGameStats;
    private DateTime gameStartTime;
    
    // Message queue for delayed sending
    private readonly Queue<string> messageQueue = new();
    private readonly object messageQueueLock = new object();
    private bool isProcessingMessages = false;
    private DateTime lastMessageSent = DateTime.MinValue;
    private const int MessageDelayMs = 2000;


    public Plugin()
    {
        // Initialize ECommons
        ECommonsMain.Init(PluginInterface, this);
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

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
        
        // Clear message queue
        lock (messageQueueLock)
        {
            messageQueue.Clear();
            isProcessingMessages = false;
        }

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
        
        // Dispose ECommons
        ECommonsMain.Dispose();
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
        if (!isGameActive || isGamePaused) return;

        var messageText = message.TextValue;
        
        // Debug logging for roll detection
        if (messageText.Contains("Random!"))
        {
            Log.Information($"[Debug] Detected Random message: '{messageText}'");
        }
        
        // Detect roll patterns (normal and debug)
        Match rollMatch;
        string playerName;
        int rollValue;
        bool isDebugRoll = false;

        if (Configuration.DebugMode)
        {
            // Try debug pattern first
            rollMatch = Regex.Match(messageText, @"Random! (.+) rolls? a (\d+) \(out of \d+\)\.");
            if (rollMatch.Success)
            {
                isDebugRoll = true;
            }
            else
            {
                // Fall back to normal pattern
                rollMatch = Regex.Match(messageText, @"Random! (.+) rolls? a (\d+)\.");
            }
        }
        else
        {
            // Normal pattern only
            rollMatch = Regex.Match(messageText, @"Random! (.+) rolls? a (\d+)\.");
        }

        if (!rollMatch.Success) 
        {
            if (messageText.Contains("Random!"))
            {
                Log.Warning($"[Debug] Random message failed regex match: '{messageText}'");
            }
            return;
        }

        playerName = rollMatch.Groups[1].Value;
        rollValue = int.Parse(rollMatch.Groups[2].Value);
        
        Log.Information($"[Debug] Roll detected - Player: '{playerName}', Value: {rollValue}");

        // Extract and format player name with server
        var normalizedName = ExtractPlayerNameWithServer(playerName);
        
        // Track all rolls for statistics
        lock (lockObject)
        {
            allRolls.Add(new RollData
            {
                PlayerName = normalizedName,
                RollValue = rollValue,
                RollTime = DateTime.Now,
                IsDebugRoll = isDebugRoll
            });
            
            participants.Add(normalizedName);
            UpdateGameStats();
        }

        // CRITICAL: Only process if this is a WINNING number
        var activeWinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount);
        Log.Information($"[Debug] Checking if {rollValue} is in winning numbers: [{string.Join(", ", activeWinningNumbers)}]");
        
        if (!activeWinningNumbers.Contains(rollValue))
        {
            Log.Information($"[Debug] Roll {rollValue} is not a winning number, ignoring");
            return; // Ignore non-winning rolls completely
        }
        
        Log.Information($"[Debug] Roll {rollValue} IS a winning number!");

        lock (lockObject)
        {
            bool canWin = false;
            
            if (Configuration.AllowMultipleWinners)
            {
                // Multiple winners mode: Check if this specific number already has a winner
                canWin = !gameWinners.Any(w => w.RollValue == rollValue);
                
                // Check if this player already won a different number (only if not allowing same player multiple wins)
                if (canWin && !Configuration.AllowSamePlayerMultipleWins && gameWinners.Any(w => w.PlayerName == normalizedName))
                {
                    Log.Information($"[Debug] Player {normalizedName} already won with a different number, ignoring");
                    return;
                }
            }
            else
            {
                // Single winner mode: Check if anyone has won yet
                canWin = gameWinners.Count == 0;
            }

            if (!canWin)
            {
                if (Configuration.AllowMultipleWinners)
                {
                    Log.Information($"[Debug] Number {rollValue} already has a winner, ignoring");
                }
                else
                {
                    Log.Information($"[Debug] Game already has a winner, ignoring");
                }
                return;
            }

            // This is a winner! Record it
            var winner = new Winner
            {
                PlayerName = normalizedName,
                RollValue = rollValue,
                WinTime = DateTime.Now,
                RollOrder = rollCounter++,
                IsDebugRoll = isDebugRoll
            };

            gameWinners.Add(winner);

            // Play winner sound
            if (Configuration.EnableWinnerSound)
            {
                PlayWinnerSound(Configuration.SelectedSoundEffect);
            }

            // Announce winner
            var debugInfo = isDebugRoll ? " [DEBUG]" : "";
            if (Configuration.UseCustomTemplates)
            {
                var announcement = Configuration.WinnerAnnouncementTemplate
                    .Replace("{player}", normalizedName)
                    .Replace("{roll}", rollValue.ToString());
                SendChatMessage($"{announcement}{debugInfo}");
            }
            else
            {
                SendChatMessage($"WINNER: {normalizedName} rolled {rollValue}!{debugInfo}");
            }

            // Auto-close logic
            if (Configuration.AutoCloseAfterFirstWinner && !Configuration.AllowMultipleWinners && gameWinners.Count == 1)
            {
                EndGame();
            }
            else if (Configuration.AllowMultipleWinners)
            {
                // Check if all winning numbers have been claimed
                var allWinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount);
                var claimedNumbers = gameWinners.Select(w => w.RollValue).ToHashSet();
                if (allWinningNumbers.All(num => claimedNumbers.Contains(num)))
                {
                    SendChatMessage("[Spamroll] All winning numbers have been claimed! Game complete.");
                    EndGame();
                }
            }
        }
    }

    private string ExtractPlayerNameWithServer(string playerName)
    {
        // Handle "You" case first
        if (playerName.Trim() == "You" && !string.IsNullOrEmpty(Configuration.LocalPlayerName))
        {
            return Configuration.LocalPlayerName;
        }

        // Check each server name from the shared library
        foreach (var server in ServerData.AllServers)
        {
            // Handle "NameServer" format (no separator)
            if (playerName.EndsWith(server, StringComparison.OrdinalIgnoreCase))
            {
                var index = playerName.LastIndexOf(server, StringComparison.OrdinalIgnoreCase);
                var nameWithoutServer = playerName.Substring(0, index).Trim();
                
                // Only format if we actually found and removed a server name
                if (!string.IsNullOrWhiteSpace(nameWithoutServer))
                {
                    return $"{nameWithoutServer} ({server})";
                }
            }
        }

        // No server found, return as-is
        return playerName.Trim();
    }

    private void PlayWinnerSound(int soundEffect)
    {
        try
        {
            if (Configuration.SoundType == SoundEffectType.GameSoundEffect)
            {
                // Play FFXIV in-game sound effect using ECommons Chat.SendMessage
                try 
                {
                    var command = $"/echo <se.{soundEffect}>";
                    Chat.SendMessage(command);
                    Log.Information($"Playing FFXIV in-game sound effect: {command}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to send sound effect command: {ex.Message}");
                }
            }
            else
            {
                // Play Windows system sound based on the selected sound effect
                // MessageBeep sound types: 0 = default, 0x10 = stop, 0x20 = question, 0x30 = exclamation, 0x40 = asterisk
                uint soundType = soundEffect switch
                {
                    1 => 0x30, // Exclamation
                    2 => 0x00, // Default beep
                    3 => 0x40, // Asterisk (informational)
                    4 => 0x20, // Question
                    5 => 0x10, // Stop/Error
                    _ => 0x30  // Default to exclamation
                };
                
                MessageBeep(soundType);
                Log.Information($"Playing Windows system sound {soundEffect} (type: 0x{soundType:X})");
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to play winner sound: {ex.Message}");
        }
    }

    public void PlayTestSound(int soundEffect)
    {
        PlayWinnerSound(soundEffect);
    }
    
    private string GetChatCommand(ChatChannel channel)
    {
        return channel switch
        {
            ChatChannel.Say => "/say ",
            ChatChannel.Party => "/party ",
            ChatChannel.Yell => "/yell ",
            ChatChannel.Shout => "/shout ",
            _ => "/say "
        };
    }
    
    private void SendChatMessage(string message)
    {
        lock (messageQueueLock)
        {
            messageQueue.Enqueue(message);
            
            if (!isProcessingMessages)
            {
                isProcessingMessages = true;
                _ = Task.Run(ProcessMessageQueue);
            }
        }
    }
    
    private async Task ProcessMessageQueue()
    {
        while (true)
        {
            string? messageToSend = null;
            
            lock (messageQueueLock)
            {
                if (messageQueue.Count == 0)
                {
                    isProcessingMessages = false;
                    return;
                }
                
                messageToSend = messageQueue.Dequeue();
            }
            
            // Ensure we wait at least 2 seconds between messages
            var timeSinceLastMessage = DateTime.Now - lastMessageSent;
            if (timeSinceLastMessage.TotalMilliseconds < MessageDelayMs)
            {
                var delayNeeded = MessageDelayMs - (int)timeSinceLastMessage.TotalMilliseconds;
                await Task.Delay(delayNeeded);
            }
            
            // Send the message
            try
            {
                var command = GetChatCommand(Configuration.AnnouncementChannel) + messageToSend;
                Chat.SendMessage(command);
                lastMessageSent = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to send chat message: {ex.Message}");
            }
        }
    }
    
    private void UpdateGameStats()
    {
        if (currentGameStats == null) return;
        
        currentGameStats.TotalParticipants = participants.Count;
        currentGameStats.TotalRolls = allRolls.Count;
        
        if (allRolls.Count > 0)
        {
            currentGameStats.MinRoll = allRolls.Min(r => r.RollValue);
            currentGameStats.MaxRoll = allRolls.Max(r => r.RollValue);
            currentGameStats.AverageRoll = allRolls.Average(r => r.RollValue);
        }
    }
    
    private void SaveGameToHistory()
    {
        if (!Configuration.SaveGameHistory || currentGameStats == null) return;
        
        var historyEntry = new GameHistoryEntry
        {
            StartTime = gameStartTime,
            EndTime = DateTime.Now,
            WinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount).ToList(),
            Winners = new List<Winner>(gameWinners),
            Stats = currentGameStats
        };
        
        Configuration.GameHistory.Insert(0, historyEntry);
        
        // Limit history size
        while (Configuration.GameHistory.Count > Configuration.MaxHistoryEntries)
        {
            Configuration.GameHistory.RemoveAt(Configuration.GameHistory.Count - 1);
        }
        
        Configuration.Save();
    }


    private void OnRollDetected(RollEventArgs rollArgs)
    {
        // This method is no longer used in the new winner-only logic
    }

    private void OnNewRollAdded(RollEventArgs rollArgs)
    {
        // This method is no longer used in the new winner-only logic
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
            isGamePaused = false;
            gameWinners.Clear();
            allRolls.Clear();
            participants.Clear();
            rollOrder.Clear();
            rollCounter = 0;
            gameStartTime = DateTime.Now;
            
            currentGameStats = new GameStats
            {
                GameStartTime = gameStartTime
            };

            var activeWinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount);
            var winningNumbersText = string.Join(", ", activeWinningNumbers.OrderBy(n => n));
            
            if (Configuration.UseCustomTemplates)
            {
                var startMessage = Configuration.GameStartTemplate.Replace("{numbers}", winningNumbersText);
                SendChatMessage(startMessage);
            }
            else
            {
                SendChatMessage($"[Spamroll] Game started! Winning numbers: {winningNumbersText}");
            }
            SendChatMessage("[Spamroll] Players, type /random to participate!");

            if (Configuration.RollTimeout > 0)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(Configuration.RollTimeout * 1000, gameCancellation.Token);
                        if (isGameActive && gameWinners.Count == 0)
                        {
                            SendChatMessage("[Spamroll] Time's up! No winners this round.");
                            EndGame();
                        }
                    }
                    catch (OperationCanceledException) { }
                });
            }
        }
    }

    public void StopGame()
    {
        EndGame();
    }
    
    public void ClearMessageQueue()
    {
        lock (messageQueueLock)
        {
            messageQueue.Clear();
        }
    }

    private void EndGame()
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
            isGamePaused = false;
            
            if (currentGameStats != null)
            {
                currentGameStats.GameEndTime = DateTime.Now;
            }

            var winnerCount = gameWinners.Count;
            
            // Save to history
            SaveGameToHistory();

            if (Configuration.UseCustomTemplates)
            {
                var endMessage = Configuration.GameEndTemplate.Replace("{winnerCount}", winnerCount.ToString());
                SendChatMessage(endMessage);
            }
            else if (Configuration.AllowMultipleWinners && winnerCount > 0)
            {
                // Show detailed results for multiple winners
                SendChatMessage($"[Spamroll] Game stopped. {winnerCount} winners:");
                foreach (var winner in gameWinners.OrderBy(w => w.RollValue))
                {
                    SendChatMessage($"  {winner.PlayerName} won {winner.RollValue}");
                }
            }
            else
            {
                SendChatMessage($"[Spamroll] Game stopped. {winnerCount} winners.");
            }
        }
    }

    public bool IsGameActive => isGameActive;
    public bool IsGamePaused => isGamePaused;
    public IReadOnlyList<Winner> GetCurrentWinners() => gameWinners;
    public GameStats? GetCurrentGameStats() => currentGameStats;
    public IReadOnlyList<RollData> GetAllRolls() => allRolls;
    public int GetParticipantCount() => participants.Count;
    
    public void PauseGame()
    {
        if (isGameActive && !isGamePaused)
        {
            isGamePaused = true;
            SendChatMessage("[Spamroll] Game paused.");
        }
    }
    
    public void ResumeGame()
    {
        if (isGameActive && isGamePaused)
        {
            isGamePaused = false;
            SendChatMessage("[Spamroll] Game resumed.");
        }
    }
    
    public void RestartGame()
    {
        if (isGameActive)
        {
            StopGame();
        }
        StartGame();
    }
    
    public void AnnounceWinners()
    {
        if (gameWinners.Count == 0)
        {
            SendChatMessage("[Spamroll] No winners to announce.");
            return;
        }
        
        SendChatMessage($"[Spamroll] Current winners ({gameWinners.Count}):");
        foreach (var winner in gameWinners.OrderBy(w => w.WinTime))
        {
            SendChatMessage($"  {winner.PlayerName} - {winner.RollValue}");
        }
    }
    
    public void ClearHistory()
    {
        Configuration.GameHistory.Clear();
        Configuration.Save();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
