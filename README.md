# Spamroll Giveaway Plugin

Run giveaways, Brain off!

Automates spamroll giveaway games in FFXIV by tracking rolls and determining winners automatically. No more manual counting or forgetting who won what numbers!

## 🎯 What It Does

- Automatically detects `/random` rolls during your giveaway games
- Configurable winning numbers (default: 111, 222, 333, 444, 555, 666, 777, 888, 999)
- Smart multiple winner support - one winner per winning number
- Prevents duplicate winners across numbers (configurable)
- Announces winners automatically to chat
- Rate-limited chat messages to avoid FFXIV spam detection
- Chat channel selection (Say, Party, Yell, Shout)
- Custom announcement templates

## 📥 Installation

1. Add this repository URL to your Dalamud plugin sources:
   ```
   https://raw.githubusercontent.com/kirin-xiv/SpamrollGiveaway-Release/main/repo.json
   ```

2. Install "Spamroll Giveaway" from the plugin installer

3. Type `/spamroll` to open the plugin window

## ⚙️ Setup

### Configuration
1. Open the plugin with `/spamroll config`
2. Set your winning numbers (1-9 numbers supported)
3. Choose your chat channel (Say, Party, Yell, Shout)
4. Configure multiple winner settings
5. Customize announcement templates (optional)

### Game Presets
- **Triple Numbers**: 111, 222, 333, 444, 555, 666, 777, 888, 999
- **Quick Roll**: 1, 50, 100 (for faster games)
- **Single Winner**: 777 only
- Save your own custom presets

## 🎮 How to Use

1. **Start a game**: Use `/spamstart` or click "Start Game" in the plugin window
2. **Watch for rolls**: The plugin automatically detects winning rolls
3. **Winners announced**: Winners are automatically announced to your chosen chat channel
4. **Game ends**: Automatically or manually with `/spamstop`

### Example Flow
```
[Spamroll] Game started! Winning numbers: 111, 222, 333 - Type /random to participate!
WINNER: PlayerName rolled 222!
WINNER: AnotherPlayer rolled 111!
[Spamroll] Game stopped. 2 winners.
```

## 🔧 Commands

- `/spamroll` - Open the main plugin window
- `/spamroll config` - Open configuration directly  
- `/spamstart` - Start collecting rolls
- `/spamstop` - Stop the current game
- `/spamconfig` - Open configuration window

## 📋 Features

### Smart Winner Detection
- Only processes configured winning numbers
- First come, first served for each number
- Optional: Same player can win multiple numbers
- Automatic game completion when all numbers claimed

### Rate-Limited Messaging
- 2-second delays between chat messages
- Prevents FFXIV chat spam detection
- Queued announcements for multiple winners
- "Clear Queue" button to cancel pending messages

### Flexible Configuration
- 1-9 winning numbers supported
- Multiple winner modes
- Custom chat templates with placeholders
- Game timeout settings
- Sound notifications

### Chat Integration
- Choose between Say, Party, Yell, Shout channels
- Custom templates: `{player}`, `{roll}`, `{numbers}`, `{winnerCount}`
- Automatic rate limiting prevents chat blocks

## 🛠️ Troubleshooting

**Plugin not detecting rolls?**
- Make sure you're using `/random` (not `/random 1000`)
- Check Debug Mode is off unless testing
- Verify Local Player Name is set correctly

**Messages getting rate limited?**
- Plugin automatically spaces messages 2 seconds apart
- Use "Clear Queue" if you need to cancel pending announcements
- Switch to a less crowded chat channel if needed

**Game not starting?**
- Make sure you have at least one winning number configured
- Check that you're not already in an active game

## 💡 Tips

- Keep the plugin window open to monitor game progress
- Use presets for quick game setup
- Progress bar shows completion in multiple winner mode
- Winner sorting options: Win Time, Roll Value, Player Name
- Copy winner names with the "Copy" button next to each winner

## 🎭 Perfect for FFXIV Events!

This plugin was designed for FFXIV event organizers running giveaway games. It eliminates manual work and ensures fair, consistent games every time. The rate limiting system means your messages won't get blocked, and the flexible winning number system works for any type of giveaway!

**Made with ❤️ by Kirin**
