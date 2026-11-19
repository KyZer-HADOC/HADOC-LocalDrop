# Release notes

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
