# Play ASHFALL on a Meta Quest

**Current version: r19** (1.3.18-playtest-r19). Everyone in a multiplayer match needs this same version.

## 1. Download the game (on your computer)

**[Download AshfallUnityQuest-1.3.18-playtest-r19.apk](https://drive.google.com/file/d/1YNJDkZMqWC8oab6-uml0TiTga1-ZjBSL/view)** (2.7 GB)

- Click the download arrow at the top of the Google Drive page.
- Google says it "can't scan this file for viruses" because the file is big. Click **Download anyway**.
- Download it to your computer (Windows, Mac or Linux), not to the headset.
- No Google account is needed. All versions are in the [ASHFALL Drive folder](https://drive.google.com/drive/folders/1QeGxet4-fbRLOVT6Gc7maXNHUxReXewm).

## 2. One-time setup (about 10 minutes)

The game is not on the Meta store, so it is installed from a computer ("sideloading").

1. **Turn on developer mode**:
   - Sign in at [developers.meta.com/horizon](https://developers.meta.com/horizon/) with your Meta account.
   - Create a developer organization. It's free and any name works. Meta may ask you to verify your account.
   - In the **Meta Horizon** phone app, go to Menu → Devices → your headset → Headset settings → **Developer mode** and turn it on.
   - Restart the headset.
2. **Install SideQuest** on your computer: go to [sidequestvr.com](https://sidequestvr.com/setup-howto) and get the **Advanced Installer** (the desktop app).

## 3. Install

1. Plug the headset into the computer with a USB-C cable. Charge-only cables don't work; the one that came with the Quest does.
2. Put the headset on. When it asks **"Allow USB debugging?"**, tick **Always allow from this computer** and press **Allow**.
3. Open SideQuest. The dot in its top-left corner turns **green** when it sees the headset.
4. **Drag the `.apk` file onto the SideQuest window.** You can also click the "Install APK file from folder on computer" icon (box with a down arrow) at the top. The install takes a few minutes. You'll need about **6 GB free** on the headset.
5. In the headset, open the **App Library**, switch the filter at the top right from "All" to **Unknown Sources**, and start **ASHFALL r19**.

## Playing together

- Choose **MULTIPLAYER** in the main menu.
- **Host** a room, or **join** one from the public room list or with the host's room code.
- Set your player name in **Settings**.

## Updating to a newer version

Download the new APK and install it the same way. It replaces the old version and keeps your settings.

If you installed the old r11 build, it is a separate app called "ASHFALL Unity Quest". It can't play with r19, so you can uninstall it from the App Library (the ⋯ next to it → Uninstall).

## Trouble?

- **SideQuest dot stays red or orange, or says "unauthorized":**
  - Put the headset on and accept the USB debugging prompt.
  - Try another cable or USB port.
  - On Windows, install Meta's **Oculus ADB Drivers** (search for that name), then reconnect.
- **No "Allow USB debugging" prompt:** developer mode isn't on yet. Turn it on in the phone app and restart the headset.
- **Install fails with `INSUFFICIENT_STORAGE`:** free some space on the headset.
- **Install fails with `UPDATE_INCOMPATIBLE`:** uninstall the old ASHFALL r-build first, then install again.
- **Can't find the game:** it only appears under **Unknown Sources** in the App Library.

### Install without SideQuest

- **adb** (Android platform tools): `adb install -r AshfallUnityQuest-1.3.18-playtest-r19.apk`
- **Meta Quest Developer Hub**: drag the APK onto the device.

### Details

- Headsets: made and tested on Quest 3. Quest 3S should behave the same. Quest 2 and Pro may install but are untested.
- Android app id: `org.alingold.ashfallunityquest.r12`, version code 29.
- SHA-256: `bb91e45e027fd1c99ee92f496e912fb077e9cafd6962b99189d4bcc88754d34b`
