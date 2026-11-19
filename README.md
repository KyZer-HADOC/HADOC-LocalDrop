# HADOC LocalDrop

**Ultra-fast local file sharing and chat between one Windows PC and one mobile phone.**

HADOC LocalDrop is a Windows 10/11 desktop application. The PC hosts a private local web interface; the phone opens it by scanning a QR code with its camera. The phone does not need a separate app.

## Scope

- Windows 10 / Windows 11 PC app
- PC ↔ Mobile phone only
- PC-to-PC and phone-to-phone sharing are intentionally not supported
- QR-based pairing
- Two-way file transfer
- Local chat
- Wi-Fi / Ethernet LAN and phone or Windows hotspot networks
- No internet or cloud upload is required when both devices are on the same local network

## How it works

1. Install and open HADOC LocalDrop on the Windows PC.
2. Connect the PC and phone to the same Wi-Fi network, or connect one device to the other device's hotspot.
3. Click **Start LocalDrop**.
4. Choose the network address that the phone can reach, then scan the QR code using the phone camera.
5. The LocalDrop phone workspace opens in the phone browser.
6. Send files from the phone to the PC, download files shared by the PC, or use Chat.

Files sent from the phone are saved to:

Downloads/HADOC LocalDrop/Received

To send a file from the PC to the phone, add it in the Windows app's **Share Files** tab. It will then appear under **Shared from your PC** on the phone.

## Offline use

The app serves the phone interface directly from the Windows PC. The QR connection, chat, and file transfer run over the local network. A working internet connection is not required.

Supported network arrangements:
- Both devices connected to the same Wi-Fi router, even when that router has no internet access.
- Windows Mobile Hotspot enabled on the PC, with the phone connected to it.
- Phone hotspot enabled, with the PC connected to the phone hotspot.

If the PC has more than one network adapter, select the matching address in the Windows app before scanning the QR code.

## Speed

The first version streams file data directly between the phone browser and the PC over the local network. It does not route files through cloud storage. Actual speed depends on the Wi-Fi generation, signal quality, router or hotspot, phone, and PC storage. Gigabit Ethernet has a theoretical ceiling of 125 MB/s before protocol overhead; Wi-Fi results vary substantially.

Bluetooth is not used as a speed booster in V1. Bluetooth does not make an active Wi-Fi transfer faster, and mobile browsers do not offer a consistent cross-device API for transferring arbitrary files over Bluetooth. Wi-Fi remains the preferred high-throughput path.

## Security notes

- Each time the PC server starts, it creates a fresh random pairing token in the QR URL.
- Keep the QR code and connection link private.
- The app is intended for a trusted local network. Use a private/trusted Wi-Fi network and allow the app through Windows Firewall only on Private networks when prompted.
- Stopping the server invalidates the current QR token.
- Do not expose the LocalDrop port to the public internet.

## Build from source

Requirements:
- Windows 10/11 x64
- .NET 8 SDK
- Inno Setup 6 (only for creating the installer)

Build the desktop app:

    dotnet restore src/HADOC.LocalDrop/HADOC.LocalDrop.csproj

    dotnet publish src/HADOC.LocalDrop/HADOC.LocalDrop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o src/HADOC.LocalDrop/bin/Release/net8.0-windows/win-x64/publish

Then compile installer/LocalDrop.iss with Inno Setup 6. The installer output is artifacts/HADOC-LocalDrop-Setup.exe.

## Download the Setup.exe from GitHub Actions

Every push to main runs the Windows build workflow:
1. Open the repository's **Actions** tab.
2. Open the latest **Build Windows Setup** run.
3. Download the **HADOC-LocalDrop-Setup** artifact.

Pushing a version tag such as v1.0.0 also creates a GitHub Release with the installer attached.

## Current V1 limits

- One phone can be paired with the PC at a time.
- The phone client runs in a modern mobile browser; no Android app is required.
- The first release uses the active local IP network. Wi-Fi Direct automation and Bluetooth transport are future enhancements, not part of this initial build.
