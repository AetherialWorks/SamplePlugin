using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Linq;
using FFXIVSharedLibrary.Chat;

namespace SpamrollGiveaway.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;

    public MainWindow(Plugin plugin)
        : base("Spamroll Giveaway##SpamrollMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Enhanced header section
        DrawGameStatus();
        
        ImGui.Separator();
        
        // Quick action buttons
        DrawQuickActions();
        
        ImGui.Separator();
        
        // Show current winning numbers clearly
        DrawWinningNumbers();
        
        ImGui.Spacing();
        
        // Enhanced winners display
        DrawWinnersSection();
    }
    
    private void DrawGameStatus()
    {
        // Game status with enhanced visual indicators
        if (Plugin.IsGameActive)
        {
            if (Plugin.IsGamePaused)
            {
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Game Paused");
            }
            else
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "Game Active");
                
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Game Inactive");
        }
        
    }
    
    private void DrawQuickActions()
    {
        // Main game controls
        if (Plugin.IsGameActive)
        {
            if (Plugin.IsGamePaused)
            {
                if (ImGui.Button("Resume"))
                {
                    Plugin.ResumeGame();
                }
                ImGui.SameLine();
            }
            else
            {
                if (ImGui.Button("Pause"))
                {
                    Plugin.PauseGame();
                }
                ImGui.SameLine();
            }
            
            if (ImGui.Button("Stop Game"))
            {
                Plugin.StopGame();
            }
            ImGui.SameLine();
            
            if (ImGui.Button("Restart"))
            {
                Plugin.RestartGame();
            }
        }
        else
        {
            if (ImGui.Button("Start Game"))
            {
                Plugin.StartGame();
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Settings"))
        {
            Plugin.ToggleConfigUI();
        }
        
        // Additional quick actions
        if (Plugin.GetCurrentWinners().Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Announce Winners"))
            {
                Plugin.AnnounceWinners();
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Clear Queue"))
        {
            Plugin.ClearMessageQueue();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Clear pending chat messages and reset timer");
    }
    
    
    private void DrawWinningNumbers()
    {
        var activeWinningNumbers = Plugin.Configuration.WinningNumbers.Take(Plugin.Configuration.WinningNumberCount);
        ImGui.Text($"Winning Numbers: {string.Join(", ", activeWinningNumbers)}");
        
        // Progress bar for multiple winners mode
        if (Plugin.Configuration.ShowProgressBar && Plugin.Configuration.AllowMultipleWinners && Plugin.IsGameActive)
        {
            var gameWinners = Plugin.GetCurrentWinners();
            var progress = (float)gameWinners.Count / Plugin.Configuration.WinningNumberCount;
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"{gameWinners.Count}/{Plugin.Configuration.WinningNumberCount} claimed");
        }
    }
    
    private void DrawWinnersSection()
    {
        var gameWinners = Plugin.GetCurrentWinners();
        
        if (gameWinners.Count > 0)
        {
            var headerText = Plugin.Configuration.AllowMultipleWinners 
                ? $"Winners This Round ({gameWinners.Count} of {Plugin.Configuration.WinningNumberCount}):"
                : "Winners This Round:";
            ImGui.Text(headerText);
            
            // Sort options
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            var sortOrder = (int)Plugin.Configuration.WinnerSortOrder;
            if (ImGui.Combo("##Sort", ref sortOrder, "Win Time\0Roll Value\0Player Name\0"))
            {
                Plugin.Configuration.WinnerSortOrder = (WinnerSortOrder)sortOrder;
                Plugin.Configuration.Save();
            }
            
            ImGui.Spacing();

            if (ImGui.BeginTable("WinnersTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Winning Roll", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Win Time", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableHeadersRow();

                var sortedWinners = Plugin.Configuration.WinnerSortOrder switch
                {
                    WinnerSortOrder.WinTime => gameWinners.OrderBy(w => w.WinTime),
                    WinnerSortOrder.RollValue => gameWinners.OrderBy(w => w.RollValue),
                    WinnerSortOrder.PlayerName => gameWinners.OrderBy(w => w.PlayerName),
                    _ => gameWinners.OrderBy(w => w.WinTime)
                };

                foreach (var winner in sortedWinners)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(winner.PlayerName);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), winner.RollValue.ToString());
                    
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(winner.WinTime.ToString("HH:mm:ss"));
                    
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton($"Copy##{winner.PlayerName}"))
                    {
                        ImGui.SetClipboardText(winner.PlayerName);
                    }
                }

                ImGui.EndTable();
            }

            // Show progress for multiple winners mode
            if (Plugin.Configuration.AllowMultipleWinners && Plugin.IsGameActive)
            {
                ImGui.Spacing();
                var activeNumbers = Plugin.Configuration.WinningNumbers.Take(Plugin.Configuration.WinningNumberCount);
                var claimedNumbers = gameWinners.Select(w => w.RollValue).ToHashSet();
                var remainingNumbers = activeNumbers.Where(n => !claimedNumbers.Contains(n));
                
                if (remainingNumbers.Any())
                {
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Still needed: {string.Join(", ", remainingNumbers)}");
                }
                else
                {
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), "All winning numbers claimed!");
                }
            }
        }
        else if (Plugin.IsGameActive)
        {
            ImGui.TextDisabled("No winners yet - waiting for winning numbers...");
        }
        else
        {
            ImGui.TextDisabled("Start a game to see winners");
        }
    }
    
}
