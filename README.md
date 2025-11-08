# LiveStrike 🎯

*A modern, sleek CS2 match tracking widget for real-time score monitoring*

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
📸 Screenshots
<p align="center"> <img src="https://github.com/babymonie/LiveStrike/blob/main/screenshots/Screenshot%202025-11-08%20220433.png" width="420" /> <img src="https://github.com/babymonie/LiveStrike/blob/main/screenshots/Screenshot%202025-11-08%20220422.png" width="420" /> </p>

## 🚀 What is LiveStrike?

LiveStrike is the ultimate sidebar companion for Counter-Strike 2 enthusiasts. Designed as a clean, non-intrusive widget, it keeps you connected to live match data without disrupting your gameplay or workflow.

**Perfect for:**
- 🎮 Gamers who want to track their favorite teams while playing
- 📺 Streamers needing live match data for content creation  
- 🏆 Esports fans following tournament matches in real-time
- 📊 Analysts monitoring multiple matches simultaneously

## ✨ Key Features

### 🎯 **Real-Time Match Data**
- Live scores and round updates from HLTV
- Team information and player statistics
- Match status and timing
- Tournament bracket progression

### 🎨 **Modern Widget Design**
- Sleek sidebar positioning
- Customizable transparency and opacity
- Clean, minimal interface that won't distract
- Dark theme optimized for gaming setups

### ⚡ **Smart & Lightweight**
- Auto-starts with Windows (optional)
- System tray integration for easy access
- Minimal resource usage (<50MB RAM)
- Zero-configuration setup - just run and go

### 🛠️ **Professional Features**
- Configurable polling intervals
- Settings persistence
- Hotkey support for quick toggle
- Self-contained deployment (no dependencies)

## 📥 Quick Start

### Download & Run
1. Download the latest release from [Releases](../../releases)
2. Run `LiveStrike.exe` - that's it!
3. **If prompted for Node.js**: Click "Yes" to install it for live data fetching
4. Right-click the system tray icon for settings

### First Launch
- LiveStrike automatically starts its data service
- If Node.js isn't installed, you'll get a helpful setup prompt
- Choose your preferred matches from the picker
- Adjust opacity and position to your liking
- Widget stays on top for continuous monitoring

## ⚙️ Settings & Customization

Access settings via system tray → **Settings**

- **🎬 Animations**: Enable/disable smooth transitions
- **👁️ Opacity**: Adjust transparency when not hovering
- **⏱️ Update Interval**: Control how often data refreshes
- **🚀 Auto-Start**: Launch with Windows
- **🎨 Theme**: Dark/Light theme selection
- **📌 Position**: Save and restore widget placement

## 🏗️ Architecture

**Frontend**: WPF (.NET 9.0) - Modern Windows UI framework  
**Backend**: Node.js + Puppeteer - Reliable HLTV data scraping  
**Data Source**: HLTV.org - The definitive CS2 match platform

## 🚀 For Developers

### Building from Source
```bash
# Clone the repository
git clone https://github.com/yourusername/livestrike.git
cd livestrike

# Restore dependencies
dotnet restore

# Build and run
dotnet run
```

### Publishing
```bash
# Quick build all platforms
.\publish.bat

# Or individual builds
dotnet publish -p:PublishProfile=Win64-SelfContained
```

See [PUBLISHING.md](PUBLISHING.md) for complete deployment guide.

## 📊 System Requirements

- **OS**: Windows 10/11 (x64, x86, ARM64)
- **Runtime**: None required (self-contained)
- **RAM**: ~50MB
- **Storage**: ~80MB
- **Network**: Internet connection for live data
- **Node.js**: Required for fetching live match data (auto-installed guidance provided)

## ⚠️ Troubleshooting

### "Node.js Required" Error
If you see a Node.js error when starting LiveStrike:

1. **Download Node.js** from https://nodejs.org/en/download/
2. **Install Node.js** with default settings (this adds it to your PATH)
3. **Restart LiveStrike** - the app will automatically detect and use Node.js
4. **Alternative**: Click "Yes" when LiveStrike prompts to open the download page

### Common Issues
- **"Failed to load matches"**: Usually means Node.js isn't installed
- **Empty match list**: Check your internet connection
- **Server not starting**: Try running as administrator or check antivirus settings

### Getting Help
- Check the logs in `%LOCALAPPDATA%\LiveStrike\app.log`
- Report issues on [GitHub Issues](../../issues)
- Include your log file when reporting problems

## 🤝 Contributing

We welcome contributions! Whether it's:
- 🐛 Bug reports and fixes
- ✨ New feature suggestions
- 📖 Documentation improvements
- 🎨 UI/UX enhancements

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

---

## 🎯 Why LiveStrike?

In the fast-paced world of CS2 esports, staying connected to live matches shouldn't mean alt-tabbing away from your game or cluttering your screen with multiple browser tabs. LiveStrike solves this with a purpose-built widget that delivers exactly what you need: **real-time scores in a clean, sidebar format**.

Whether you're grinding ranked matches while keeping an eye on the latest tournament, streaming content with live match overlays, or analyzing team performances, LiveStrike keeps you informed without getting in your way.

**Ready to stay ahead of the game?** Download LiveStrike and never miss a moment of CS2 action.

---

*Made with ❤️ for the CS2 community*
