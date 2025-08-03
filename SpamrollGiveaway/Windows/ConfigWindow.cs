using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Linq;

namespace SpamrollGiveaway.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;
    private int[] winningNumberInputs = new int[5];

    public ConfigWindow(Plugin plugin) : base("Spamroll Giveaway Configuration###SpamrollConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;

        Configuration = plugin.Configuration;
        Plugin = plugin;

        // Initialize input fields with current winning numbers
        for (int i = 0; i < 5; i++)
        {
            winningNumberInputs[i] = i < Configuration.WinningNumbers.Count ? Configuration.WinningNumbers[i] : 0;
        }
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        ImGui.Text("Configure winning numbers (0-999):");
        ImGui.Separator();

        ImGui.Text("Winning Numbers (leave 0 to disable slot):");
        
        bool numbersChanged = false;
        for (int i = 0; i < 5; i++)
        {
            ImGui.PushID(i);
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt($"#{i + 1}", ref winningNumberInputs[i]))
            {
                if (winningNumberInputs[i] < 0) winningNumberInputs[i] = 0;
                if (winningNumberInputs[i] > 999) winningNumberInputs[i] = 999;
                numbersChanged = true;
            }
            if (i < 4) ImGui.SameLine();
            ImGui.PopID();
        }

        if (numbersChanged)
        {
            Configuration.WinningNumbers.Clear();
            for (int i = 0; i < 5; i++)
            {
                if (winningNumberInputs[i] > 0)
                {
                    Configuration.WinningNumbers.Add(winningNumberInputs[i]);
                }
            }
            Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Game Settings:");
        
        var rollTimeout = Configuration.RollTimeout;
        if (ImGui.SliderInt("Roll Timeout (seconds)", ref rollTimeout, 0, 300))
        {
            Configuration.RollTimeout = rollTimeout;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("0 = No timeout");

        var autoClose = Configuration.AutoCloseAfterWin;
        if (ImGui.Checkbox("Auto-close game after first winner", ref autoClose))
        {
            Configuration.AutoCloseAfterWin = autoClose;
            Configuration.Save();
        }

        var showHistory = Configuration.ShowRollHistory;
        if (ImGui.Checkbox("Show roll history in main window", ref showHistory))
        {
            Configuration.ShowRollHistory = showHistory;
            Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Debug Settings:");
        
        var debugMode = Configuration.DebugMode;
        if (ImGui.Checkbox("Debug Mode", ref debugMode))
        {
            Configuration.DebugMode = debugMode;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Enables parsing of debug roll messages");

        var localPlayerName = Configuration.LocalPlayerName;
        if (ImGui.InputText("Local Player Name", ref localPlayerName, 50))
        {
            Configuration.LocalPlayerName = localPlayerName;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Override for 'You' in roll messages");

        var movable = Configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            Configuration.IsConfigWindowMovable = movable;
            Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (Plugin.IsGameActive)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Game is currently active!");
            if (ImGui.Button("Stop Game"))
            {
                Plugin.StopGame();
            }
        }
        else
        {
            if (Configuration.WinningNumbers.Count == 0)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "No winning numbers set!");
            }
            else
            {
                var winningText = string.Join(", ", Configuration.WinningNumbers.OrderBy(n => n));
                ImGui.Text($"Current winning numbers: {winningText}");
                
                if (ImGui.Button("Start Game"))
                {
                    Plugin.StartGame();
                }
            }
        }
    }
}
