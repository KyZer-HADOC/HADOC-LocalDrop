# HADOC LocalDrop

**Local file sharing and chat between one Windows PC and one mobile phone.**

HADOC LocalDrop turns your Windows PC into a private file-sharing station. Open the companion interface on your phone by scanning the QR code shown in the Windows app. No Android/iOS app or cloud account is required.

<p align="center">
  <strong>Windows 10 / 11 · PC ↔ Mobile · Wi-Fi / LAN · Offline-ready</strong>
</p>

## Download

**[Download HADOC LocalDrop Setup.exe](https://github.com/KyZer-HADOC/HADOC-LocalDrop/releases/latest/download/HADOC-LocalDrop-Setup.exe)**

You can also visit the [Releases page](https://github.com/KyZer-HADOC/HADOC-LocalDrop/releases) to see version notes and previous builds.

## Features in V1

- QR pairing from the Windows app to a phone browser
- Two-way file transfer between the PC and the paired phone
- Local chat between the PC and phone
- Multiple file selection from the phone
- Transfer progress feedback while sending files
- Share selected PC files for download on the phone
- Works over a shared Wi-Fi network, Ethernet LAN, Windows Mobile Hotspot, or phone hotspot
- Local-network transfer without cloud storage or internet access
- Fresh random pairing token each time the PC server starts

> **Scope:** This project intentionally supports PC ↔ Mobile only. PC-to-PC and phone-to-phone transfers are not supported.

## Install

1. Download `HADOC-LocalDrop-Setup.exe` from the link above.
2. Run the installer on a Windows 10 or Windows 11 x64 PC.
3. Complete the setup wizard and launch HADOC LocalDrop.
4. Connect your PC and phone to the same Wi-Fi network, or connect one device to the other's hotspot.
5. Click **Start LocalDrop** in the Windows app.
6. Select the network address that your phone can reach.
7. Scan the QR code with your phone camera and open the link.
8. Use the **Files** and **Chat** sections in the phone browser.

The phone does not need a separate app installation.

## Use without internet

A router can provide a local network even when its internet connection is unavailable. LocalDrop serves the phone interface directly from the Windows PC, and file data stays on that local network.

Supported arrangements:
- PC and phone connected to the same Wi-Fi router, even if the router has no internet.
- Windows Mobile Hotspot enabled on the PC and the phone connected to it.
- Phone hotspot enabled and the PC connected to the phone hotspot.
- PC connected by Ethernet and phone connected to the same router's Wi-Fi.

If the PC has multiple network adapters, choose the address that belongs to the network shared with the phone.

## Where received files are saved

Files sent from the phone are saved to:

`Downloads\HADOC LocalDrop\Received`

To send files from the PC to the phone:
1. Open the **Share Files** tab in the Windows app.
2. Click **Add files** and select the files.
3. Open **Shared from your PC** on the phone.
4. Tap **Download** beside the file.

## Speed and connectivity

V1 streams files directly over the local network; it does not upload them to a cloud service. Real-world speed depends on the Wi-Fi standard, signal strength, router or hotspot, phone, PC network adapter, and storage device.

Bluetooth is not used as a speed booster. Adding Bluetooth to an active Wi-Fi transfer does not make that Wi-Fi transfer faster. Wi-Fi is the preferred high-throughput transport. Wi-Fi Direct automation and Bluetooth-based fallback are future enhancements, not part of V1.

## Security

- The QR code contains a fresh random pairing token generated when the local server starts.
- Treat the QR code and connection URL like a password. Only scan/share it with a device you trust.
- Use a trusted private network.
- If Windows Firewall asks, allow LocalDrop on **Private networks** only.
- Stop the server when you finish sharing. Stopping it invalidates the active pairing token.
- Do not expose the LocalDrop port to the public internet.

V1 uses HTTP on the local network, not end-to-end encrypted transport. Use it only on a trusted network; encryption and stronger pairing controls are planned improvements.

## Build from source

Requirements:
- Windows 10/11 x64
- .NET 8 SDK
- Inno Setup 6 (to create the installer)

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

Every push to `main` runs the **Build Windows Setup** GitHub Actions workflow. The workflow:
1. Restores and publishes the self-contained Windows x64 app.
2. Builds the installer using Inno Setup.
3. Uploads `HADOC-LocalDrop-Setup.exe` as a workflow artifact.
4. Publishes the tagged release when a version release is requested.

Open the [Actions page](https://github.com/KyZer-HADOC/HADOC-LocalDrop/actions) to inspect build logs.

## Current V1 limitations

- One phone can be paired with the PC at a time.
- The phone companion runs in a modern mobile browser.
- Wi-Fi Direct automation and Bluetooth fallback are not yet implemented.
- Transfer speed has not yet been benchmarked across different phones and network adapters.
- The initial release is a functional starting point; further testing and optimization are ongoing.

---

**HADOC LocalDrop** · Built by **KyZer_HADOC**
