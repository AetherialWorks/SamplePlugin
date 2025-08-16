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
        // Header section
        if (Plugin.IsGameActive)
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "Game Active");
            ImGui.SameLine();
            if (ImGui.Button("Stop Game"))
            {
                Plugin.StopGame();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Game Inactive");
            ImGui.SameLine();
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

        ImGui.Separator();

        // Show current winning numbers clearly
        var activeWinningNumbers = Plugin.Configuration.WinningNumbers.Take(Plugin.Configuration.WinningNumberCount);
        ImGui.Text($"Winning Numbers: {string.Join(", ", activeWinningNumbers)}");

        ImGui.Spacing();

        // Show winners
        var gameWinners = Plugin.GetCurrentWinners();
        if (gameWinners.Count > 0)
        {
            var headerText = Plugin.Configuration.AllowMultipleWinners 
                ? $"Winners This Round ({gameWinners.Count} of {Plugin.Configuration.WinningNumberCount}):"
                : "Winners This Round:";
            ImGui.Text(headerText);
            ImGui.Spacing();

            if (ImGui.BeginTable("WinnersTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Winning Roll", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Win Time", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableHeadersRow();

                foreach (var winner in gameWinners.OrderBy(w => w.WinTime))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(winner.PlayerName);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), winner.RollValue.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(winner.WinTime.ToString("HH:mm:ss"));
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

        // Bottom buttons
        ImGui.Separator();
    }
}
