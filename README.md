Cache Cow 🐄
Cache Cow is an enterprise-grade, cross-platform system and cache cleaning utility. Originally built as a PowerShell script suite, it has been fully architected into a standalone, compiled .NET 8 application. It provides safe, rapid, configuration-driven cleanup of system temp files, developer caches, and application data while strictly guarding protected browser and user profiles.

🚀 Key Features
Cross-Platform Compatibility: Runs natively on Windows, macOS, and Linux using a single, unified configuration file.

Native AOT Compilation: Compiles to a single, standalone executable with zero runtime dependencies. No installation of the .NET runtime is required.

Intelligent Path Resolution: Automatically translates environment variables and alias paths (e.g., %TEMP%, ~/Library, $TMPDIR) depending on the host operating system.

Safety & Protection Guards: Explicitly block protected directories (like Edge/Chrome user data) from accidental deletion.

Windows System Restore Integration: Automatically creates a System Restore Point before execution (Windows only).

Enterprise Structured Logging: Generates auditable, JSON-formatted structured logs using Serilog, ready for ingestion into SIEM tools.

Over-the-Air Auto Updates: Integrated Velopack background updater pulls the latest releases directly from GitHub.

Dry-Run Mode: Execute with --dry-run to generate a safe audit of what would be deleted without writing to the disk.

📥 Installation
Because Cache Cow is built using .NET Native AOT, you do not need to install anything on your system.

Navigate to the [suspicious link removed] page.

Download the latest compressed archive for your operating system (e.g., cachecow-win-x64.zip, cachecow-osx-x64.tar.gz).

Extract the contents to your preferred directory.

Run the executable.

Note: On Windows, Cache Cow will natively prompt for Administrator privileges via UAC to clean system-level directories.

⚙️ Configuration
Cache Cow is entirely driven by appsettings.json, located in the same directory as the executable. You can add, remove, or modify targets across different operating systems seamlessly.

JSON
{
  "UpdateSettings": {
    "EnableAutoUpdate": true,
    "ReleaseChannel": "https://api.github.com/repos/Rythorian-oss/cachecow/releases"
  },
  "Targets": [
    {
      "Name": "User Temp Folder",
      "WindowsPath": "%TEMP%",
      "MacPath": "$TMPDIR",
      "LinuxPath": "/tmp",
      "Type": "Directory",
      "IsProtected": false
    },
    {
      "Name": "Edge User Data",
      "WindowsPath": "%LOCALAPPDATA%/Microsoft/Edge/User Data",
      "MacPath": "~/Library/Application Support/Microsoft Edge",
      "LinuxPath": "~/.config/microsoft-edge",
      "Type": "Directory",
      "IsProtected": true
    }
  ]
}
Target Properties
Type: Set to Directory for standard folders, or SystemObject for OS-level integrations (like RecycleBin).

IsProtected: If set to true, Cache Cow's execution engine will block any attempt to clean this path, acting as a failsafe against misconfigurations.

💻 Usage
Run Cache Cow from your terminal to utilize execution flags:

Bash
# Run a standard cleanup
./CacheCow

# Run an audit without deleting files
./CacheCow --dry-run

# Run and keep the terminal window open after completion
./CacheCow --pause
Log Output
Logs are automatically rolled daily and saved to your local application data directory:

Windows: %LOCALAPPDATA%\CacheCow\logs\cachecow_.json

macOS: ~/Library/Application Support/CacheCow/logs/cachecow_.json

Linux: ~/.config/CacheCow/logs/cachecow_.json

🛠️ Building from Source
To build Cache Cow yourself, ensure you have the .NET 8.0 SDK installed.

Bash
# Clone the repository
git clone https://github.com/Rythorian-oss/cachecow.git
cd cachecow

# Publish for Windows (x64)
dotnet publish CacheCow.csproj -c Release -r win-x64 --self-contained -p:PublishAot=true

# Publish for macOS (Apple Silicon)
dotnet publish CacheCow.csproj -c Release -r osx-arm64 --self-contained -p:PublishAot=true

# Publish for Linux (x64)
dotnet publish CacheCow.csproj -c Release -r linux-x64 --self-contained -p:PublishAot=true
🤝 Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change. Please ensure to update tests and target validations as appropriate.

📄 License
This project is licensed under the MIT License. See the LICENSE file for details.
