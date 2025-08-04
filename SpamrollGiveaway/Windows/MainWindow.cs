using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
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

        // Only show winners (remove the full roll table)
        var gameWinners = Plugin.GetCurrentWinners();
        if (gameWinners.Count > 0)
        {
            ImGui.Text("Winners This Round:");
            ImGui.Spacing();

            if (ImGui.BeginTable("WinnersTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Winning Roll", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableHeadersRow();

                foreach (var winner in gameWinners)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(winner.PlayerName);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), winner.RollValue.ToString());
                }

                ImGui.EndTable();
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
