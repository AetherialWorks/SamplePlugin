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

        // Winning numbers display
        if (Plugin.Configuration.WinningNumbers.Count > 0)
        {
            var winningText = string.Join(", ", Plugin.Configuration.WinningNumbers.OrderBy(n => n));
            ImGui.TextUnformatted($"Winning Numbers: {winningText}");
        }
        else
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "No winning numbers configured!");
        }

        ImGui.Spacing();

        // Winners display
        var winners = Plugin.GetWinners();
        if (winners.Count > 0)
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Winners ({winners.Count}):");
            
            using (var winnersChild = ImRaii.Child("Winners", new Vector2(0, 100), true))
            {
                if (winnersChild.Success)
                {
                    foreach (var winner in winners)
                    {
                        ImGui.TextColored(new Vector4(0, 1, 0, 1), $"🎉 {winner.NormalizedPlayerName} rolled {winner.RollValue}!");
                    }
                }
            }
        }

        ImGui.Spacing();

        // Current rolls display
        var rolls = Plugin.GetCurrentRolls();
        ImGui.TextUnformatted($"Current Rolls ({rolls.Count}):");

        using (var rollsChild = ImRaii.Child("Rolls", Vector2.Zero, true))
        {
            if (rollsChild.Success)
            {
                if (rolls.Count == 0)
                {
                    ImGui.TextUnformatted("No rolls yet...");
                }
                else
                {
                    // Create table for organized display
                    if (ImGui.BeginTable("RollsTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                    {
                        ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 60);
                        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 80);
                        ImGui.TableHeadersRow();

                        foreach (var roll in rolls.Values.OrderBy(r => r.RollOrder))
                        {
                            ImGui.TableNextRow();
                            
                            ImGui.TableSetColumnIndex(0);
                            ImGui.TextUnformatted(roll.NormalizedPlayerName);
                            
                            ImGui.TableSetColumnIndex(1);
                            var isWinning = Plugin.Configuration.WinningNumbers.Contains(roll.RollValue);
                            if (isWinning)
                            {
                                ImGui.TextColored(new Vector4(0, 1, 0, 1), roll.RollValue.ToString());
                            }
                            else
                            {
                                ImGui.TextUnformatted(roll.RollValue.ToString());
                            }
                            
                            ImGui.TableSetColumnIndex(2);
                            if (isWinning)
                            {
                                ImGui.TextColored(new Vector4(1, 1, 0, 1), "WINNER!");
                            }
                            else
                            {
                                ImGui.TextUnformatted("-");
                            }
                        }
                        
                        ImGui.EndTable();
                    }
                }
            }
        }

        // Bottom buttons
        ImGui.Separator();
        if (ImGui.Button("Clear Rolls") && !Plugin.IsGameActive)
        {
            // Clear would be handled by the plugin if we exposed it
        }
    }
}
