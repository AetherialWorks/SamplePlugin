﻿using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace SpamrollGiveaway;

public enum SoundEffectType
{
    WindowsSystemSound = 0,
    GameSoundEffect = 1
}

public enum WinnerSortOrder
{
    WinTime = 0,
    RollValue = 1,
    PlayerName = 2
}

public class GameStats
{
    public int TotalParticipants { get; set; }
    public int TotalRolls { get; set; }
    public int MinRoll { get; set; } = int.MaxValue;
    public int MaxRoll { get; set; } = int.MinValue;
    public double AverageRoll { get; set; }
    public DateTime GameStartTime { get; set; }
    public DateTime? GameEndTime { get; set; }
    public TimeSpan ElapsedTime => (GameEndTime ?? DateTime.Now) - GameStartTime;
}

public class GamePreset
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<int> WinningNumbers { get; set; } = new();
    public int WinningNumberCount { get; set; }
    public bool AllowMultipleWinners { get; set; }
    public bool AllowSamePlayerMultipleWins { get; set; }
    public int RollTimeout { get; set; }
}

public class GameHistoryEntry
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<int> WinningNumbers { get; set; } = new();
    public List<Winner> Winners { get; set; } = new();
    public GameStats Stats { get; set; } = new();
    public string PresetName { get; set; } = "";
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool IsConfigWindowMovable { get; set; } = true;
    
    // Winning number configuration
    public int WinningNumberCount { get; set; } = 9;
    public List<int> WinningNumbers { get; set; } = new() { 111, 222, 333, 444, 555, 666, 777, 888, 999 };
    
    // Game settings
    public int RollTimeout { get; set; } = 30;
    public string LocalPlayerName { get; set; } = "";
    public bool AutoCloseAfterFirstWinner { get; set; } = true;
    public bool ShowRollHistory { get; set; } = false;
    public bool AllowMultipleWinners { get; set; } = false; // One winner per winning number
    public bool AllowSamePlayerMultipleWins { get; set; } = false; // Same player can win multiple numbers
    
    // Sound settings
    public bool EnableWinnerSound { get; set; } = true;
    public int SoundVolume { get; set; } = 50;
    public int SelectedSoundEffect { get; set; } = 1; // Default to <se.1>
    public SoundEffectType SoundType { get; set; } = SoundEffectType.GameSoundEffect;
    
    // Debug settings
    public bool DebugMode { get; set; } = false;
    
    // UI Settings
    public WinnerSortOrder WinnerSortOrder { get; set; } = WinnerSortOrder.WinTime;
    public bool ShowGameStats { get; set; } = true;
    public bool ShowProgressBar { get; set; } = true;
    public bool ShowElapsedTime { get; set; } = true;
    public bool AutoAnnounceWinners { get; set; } = true;
    public bool ShowParticipantCount { get; set; } = true;
    
    // Game Management
    public bool SaveGameHistory { get; set; } = true;
    public int MaxHistoryEntries { get; set; } = 50;
    public List<GameHistoryEntry> GameHistory { get; set; } = new();
    public List<GamePreset> GamePresets { get; set; } = new();
    
    // Chat Integration
    public string WinnerAnnouncementTemplate { get; set; } = "WINNER: {player} rolled {roll}!";
    public string GameStartTemplate { get; set; } = "[Spamroll] Game started! Winning numbers: {numbers}";
    public string GameEndTemplate { get; set; } = "[Spamroll] Game ended! {winnerCount} winners.";
    public bool UseCustomTemplates { get; set; } = false;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
    }
}
