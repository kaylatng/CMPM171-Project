# Cupidity Build Instructions & Setup Guide
Welcome to the **Cupidity** build! This guide will help you get the game running and connected for local multiplayer for testing or playing.

## Which Version Should I Download?
| **File Name**    | **Operating System** | 
| :-------- | :---------- | 
| Cupidity_MacOS.zip     | For **Apple/Mac** users.| 
| Cupidity_Windows.zip  | For **Windows 10/11** users. |          

## Installation & Launching
### Windows Instructions
1. Download Cupidity_Windows.zip.
1. **Right-click** the file and select **Extract All**.
1. Open the folder and double-click Cupidity.exe to launch.
    * *Note: If a "Windows protected your PC" popup appears, click **More Info** and then **Run Anyway**.*

### Mac Instructions
1. Download Cupidity_MacOS.zip and extract the application.
1. Double-click the **Cupidity** application.
1. **The app will likely be blocked.** To allow it:
    * Open **System Settings** (or System Preferences).
    * Navigate to **Privacy & Security**.
    * Scroll down to the bottom where it mentions the "Cupidity" application.
    * Click **Open Anyway** and enter your password if prompted.

## How to Connect Two Players (Local Network)**
To play together, both players must be on the **same Wi-Fi** or **Local Network**.

### Step 1: Host Setup (Person A)
1. **Find your IP:**
    * **Windows:** Press Win + R, type cmd, and press Enter. Type ipconfig and find the **IPv4 Address** (e.g., 192.168.1.15).
    * **Mac:** Open **System Settings > Network**, click on your Wi-Fi, and find the IP address listed there.
1. Launch the game.
1. Select **Play > Quickplay** (This will start the host server).
### Step 2: Guest Setup (Person B)
1. Launch the game and select **Play**.
2. Locate the **Enter Host IP** text field.
3. Type in the **IPv4 Address** provided by Person A.
4. Click **Join**.

**Opening two builds on one computer will also work, just hit **Quickplay** on one player and **Join** on the other player’s build.

## Technical System Requirements
  * **OS:** Windows 10 (64-bit) or macOS Big Sur (or newer).
  * **Processor:** Dual Core 2.0 GHz or faster.
  * **Memory:** 4 GB RAM minimum.
  * **Graphics:** Integrated graphics are fine, but dedicated cards are recommended for the best particle effects.
  * **Connection:** Stable local network connection (Ethernet or 5GHz Wi-Fi recommended).

## Common Troubleshooting
  * **Firewall Blocks:** If the game won't connect, ensure that your Firewall is not blocking Unity/Cupidity. You may need to "Allow" the app when Windows/Mac prompts you on the first run.
  * **Wrong IP:** Ensure Person B is typing the **IPv4**, not the "Default Gateway" or "IPv6" address.

## Accessibility (4 of 5)
  * **Seeing:** Added a High Contrast Grayscale Image Filter to our settings screen. This allows the player to toggle between full color and a grayscale version of the game.
  * **Touching:** Cupidity is playable with a touchpad or mouse. Only one hand is needed to play the game!
  * **Hearing:** Completable with audio muted. In the top right corner of the main game, there is a headphones icon that allows you to mute or turn on audio.
  * **Resting:** Every round lasts about 1-2 minutes and allows the player to rest and watch the game phase transition into a new phase. There is a natural resting point where players can watch their opponent’s actions without needing to pause the game.
  * **(Bonus) Localization:** Cupidity currently supports English only. We are working on implementing Spanish and Japanese, which you can see in our main menu.

## Credits
### CMPM 171 Team
  * **Kayla Nguyen** | *Production Lead & Engine Lead*
  * **Evelyn Marino** | *Tools Lead*
  * **Samantha Siew** | *Engine Lead*
  * **Manco Tan** | *Design Lead*
  * **Bhavya Anil** | *Playtesting Lead & Design Co-Lead*
### External
* **Jeremy Miller** | *Noise Background Generator*
* **Alasdair Lam** | *Background Music*
* **Grape Soda Font** | *https://www.dafont.com/grapesoda-2.font*
* **m6x11 Font** | *https://managore.itch.io/m6x11*


