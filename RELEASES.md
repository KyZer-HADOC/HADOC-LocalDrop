# Release notes

## v1.0.1 — Unified workspace and incoming file visibility

- Replaced the separate Files and Chat tabs in the phone browser with one conversation-style workspace.
- Moved the message field, Send control, and prominent Add files button to the bottom composer.
- Kept the phone on a minimal QR-pairing screen until the PC connection succeeds.
- After pairing, the Windows app switches from the QR screen to a single connected workspace.
- Added a visible **Received on PC** list and an **Open folder** shortcut.
- Phone uploads are streamed directly to the PC and now return clear success or failure feedback.
- Updated the interface styling and removed the remote-font dependency so the phone UI works without internet.
- Updated the README and app/installer version to 1.0.1.

### Notes

This is an early PC-to-mobile release. The Windows build and installer are checked by GitHub Actions. Real-world transfer speed and phone compatibility still need testing on physical devices. Wi-Fi Direct automation and Bluetooth fallback are not included yet.
