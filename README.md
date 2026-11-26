# HADOC LocalDrop

**Direct file sharing and chat between one Windows PC and one mobile phone.**

HADOC LocalDrop turns your Windows PC into a private local transfer hub. Scan the QR code shown by the Windows desktop app to open the companion workspace in your phone browser. No separate phone app, cloud account, or internet connection is required when both devices share a local network.

<p align="center">
  <strong>Windows 10 / 11 · PC ↔ Mobile · Wi-Fi / LAN · Offline-ready</strong>
</p>

## App icon

The Windows app, taskbar entry, and installer use a custom black-and-red HADOC LocalDrop icon. The Windows build generates the icon from `scripts/Generate-Icon.ps1` so the same artwork is embedded in the app and Setup.exe.

## Download

**[Download HADOC LocalDrop Setup.exe](https://github.com/KyZer-HADOC/HADOC-LocalDrop/releases/latest/download/HADOC-LocalDrop-Setup.exe)**

[View all releases](https://github.com/KyZer-HADOC/HADOC-LocalDrop/releases) · [View build logs](https://github.com/KyZer-HADOC/HADOC-LocalDrop/actions)

## Features

- QR pairing from the Windows PC app to a phone browser
- One unified chat and file-sharing workspace after connection
- Send messages between the PC and the connected phone
- Phone → PC uploads appear as file cards in the Windows conversation, with live progress and an **Open file** action when received
- PC → Phone files appear in the same conversation and can be downloaded from the phone with a visible download button
- Transfer progress and live speed updates are reported to the Windows app for both directions
- Multi-line chat messages using line breaks
- Multiple file selection from the phone
- Files received on the PC are saved to `Downloads\HADOC LocalDrop\Received`
- Private local network transfer over Wi-Fi or Ethernet
- Works with a Wi-Fi router even when that router has no internet, Windows Mobile Hotspot, or a phone hotspot
- Fresh random pairing token each time the PC server starts
- Phone companion works in a modern browser; no Android/iOS app installation

> **Scope:** PC ↔ Mobile only. PC-to-PC and phone-to-phone transfers are intentionally not supported.

## Install and connect

1. Download `HADOC-LocalDrop-Setup.exe` from the latest release.
2. Run the installer on a Windows 10 or Windows 11 x64 PC.
3. Launch HADOC LocalDrop.
4. Enter the router Wi-Fi name (SSID) and WPA/WPA2 password in the Windows app. These details are saved encrypted for the current Windows user.
5. Click **Start LocalDrop** and select the PC network address connected to that router (Ethernet is supported).
6. Open the **HADOC LocalDrop Android app** on the phone and scan the Windows QR from its **Connect to PC** option.
7. Approve Android's system Wi-Fi join prompt. The app requests a temporary router connection, then opens the PC workspace.
8. Disconnect from the workspace when finished; LocalDrop releases its temporary Wi-Fi network request so Android can restore the phone's previous network.

The phone does not need a separate app installation. The PC app shows the connected phone workspace after pairing.

## Send and receive files

### Phone → PC

1. In the phone workspace, tap **Add files** beside the message box.
2. Select one or more files.
3. Watch the upload progress in the conversation.
4. The file appears in the same Windows conversation while it uploads, with a live progress bar and speed.
5. After completion, the Windows conversation shows **Received on this PC** and an **Open file** button.

Received files are saved in:

`%USERPROFILE%\Downloads\HADOC LocalDrop\Received`

Use **Received folder** in the Windows app to view them in File Explorer.

### PC → Phone

1. In the Windows connected workspace, click **Add files**.
2. Select one or more files.
3. The files appear in the phone conversation as downloadable items.
4. Tap **Download to phone** beside the file. Its progress and speed are mirrored in the Windows conversation.

The phone checks for newly shared PC files while the workspace is open.

## Use without internet

The phone interface is served directly by the Windows PC. Pairing, chat, and file transfer use the local network and do not send file data through cloud storage.

Supported arrangements:
- PC connected to the router by Ethernet and phone initially on mobile data or another Wi-Fi network. Scan the PC QR inside the HADOC LocalDrop Android app; Android will ask permission to join the router using the credentials carried in the QR.
- PC and phone already connected to the same Wi-Fi router, even if the router has no internet. Older QR links without Wi-Fi details also work this way.
- Windows Mobile Hotspot enabled on the PC and the phone connected to it.
- Phone hotspot enabled and the PC connected to that hotspot.

If the PC has more than one network adapter, select the address that belongs to the router the phone should join. Automatic temporary router joining requires Android 10 or newer and the HADOC LocalDrop Android app; scanning in a generic camera/browser does not initiate the app-scoped Wi-Fi connection.

## Speed and connectivity

File contents are streamed directly between the phone browser and the PC over the local network. Actual speed depends on the Wi-Fi standard, signal quality, router or hotspot, phone, PC network adapter, and storage device.

Bluetooth is not used as a speed booster. Adding Bluetooth to an active Wi-Fi transfer does not make the Wi-Fi transfer faster. Wi-Fi is the preferred high-throughput transport. Wi-Fi Direct automation and Bluetooth fallback are future enhancements, not part of this release.

## Security

- A new random pairing token is generated whenever the PC server starts.
- Treat the QR code and connection URL like a password. The new QR includes the router Wi-Fi passphrase as well as the one-time LocalDrop token. Only scan/share it with a device you trust, and do not post or forward it publicly.
- The Windows app stores the router passphrase encrypted with Windows DPAPI for the current Windows account.
- Use a trusted private network.
- If Windows Firewall asks, allow LocalDrop on **Private networks** only.
- Stop LocalDrop when you finish sharing. Stopping the server invalidates the active pairing token.
- Do not expose the LocalDrop port to the public internet.

The current local web connection uses HTTP on the LAN, not end-to-end encrypted transport. Use it only on a trusted network. Stronger encrypted pairing and transport are planned improvements.

## Build from source

Requirements:
- Windows 10/11 x64
- .NET 8 SDK
- Inno Setup 6 (to create the installer)

Generate the icon before building:

```powershell
.\scripts\Generate-Icon.ps1
```

Restore:

```powershell
dotnet restore src/HADOC.LocalDrop/HADOC.LocalDrop.csproj
```

Publish:

```powershell
dotnet publish src/HADOC.LocalDrop/HADOC.LocalDrop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o src/HADOC.LocalDrop/bin/Release/net8.0-windows/win-x64/publish
```

Compile `installer/LocalDrop.iss` with Inno Setup 6. The installer is written to `artifacts/HADOC-LocalDrop-Setup.exe`.

## Continuous builds

Every push to `main` runs the **Build Windows Setup** GitHub Actions workflow. It restores and publishes the self-contained app, builds the installer, and uploads the Setup.exe artifact. Version releases attach the same installer to GitHub Releases.

## Current release limits

- One phone can be paired with the PC at a time.
- The phone companion runs in a modern mobile browser.
- Wi-Fi Direct automation and Bluetooth fallback are not implemented.
- Transfer speed has not yet been benchmarked across different phone and network combinations.
- The current release is an early functional version; device-to-device testing and further optimization are ongoing.

---

**HADOC LocalDrop** · Built by **KyZer_HADOC**
