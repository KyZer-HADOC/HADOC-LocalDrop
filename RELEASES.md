# Release notes

## v1.0.4 — One-scan temporary router Wi-Fi pairing

Windows release version: **1.0.4**.

- Added router Wi-Fi SSID and password fields to the Windows app.
- Saved the router password encrypted with Windows DPAPI for the current Windows account.
- Included the router Wi-Fi details in the private one-time pairing QR.
- Updated the Android companion app to request a temporary Android Wi-Fi connection from the scanned PC QR, then route all LocalDrop HTTP and WebSocket traffic through that selected network.
- The Android app releases its temporary Wi-Fi network request when the PC session ends.
- Kept the existing PC Ethernet/LAN server and chat/file-sharing flow.

**Security note:** the QR contains the router Wi-Fi passphrase and one-time pairing token. Only scan/share it with a trusted device; do not publish it. Android 10 or newer and the HADOC LocalDrop Android app are required for automatic temporary Wi-Fi joining.

## v1.0.3 — Unified desktop conversation and live transfers

- Rebuilt the Windows UI as a maximized, full-width conversation instead of separate chat and file panels.
- Added a polished dark charcoal and crimson theme with the HADOC LocalDrop app icon.
- Displayed phone-to-PC received files as chat cards with progress, transfer speed, and an Open file action.
- Displayed PC-to-phone shared files in the same Windows conversation and mirrored phone download progress back to the PC.
- Added live progress reporting for both transfer directions.
- Added multi-line chat on Windows and mobile using Enter / Shift+Enter conventions.
- Kept the phone pairing screen visible only until QR connection succeeds.

## v1.0.2 — Custom Windows app icon

- Added a custom black-and-red HADOC LocalDrop brand icon.
- Embedded the icon into the Windows executable for Explorer, taskbar, and window chrome.
- Applied the same icon to the Setup.exe installer and Start Menu / desktop shortcuts.
- Added a reproducible PowerShell icon generator used by GitHub Actions and local builds.

## v1.0.1 — Unified workspace and incoming file visibility

- Replaced the separate Files and Chat tabs in the phone browser with one conversation-style workspace.
- Moved the message field, Send control, and prominent Add files button to the bottom composer.
- Kept the phone on a minimal QR-pairing screen until the PC connection succeeds.
- After pairing, the Windows app switches from the QR screen to a single connected workspace.
- Added a visible **Received on PC** list and an **Open folder** shortcut.
- Phone uploads are streamed directly to the PC and now return clear success or failure feedback.
- Updated the interface styling and removed the remote-font dependency so the phone UI works without internet.

### Notes

This is an early PC-to-mobile release. The Windows build and installer are checked by GitHub Actions. Real-world transfer speed and phone compatibility still need testing on physical devices. Wi-Fi Direct automation and Bluetooth fallback are not included yet.
