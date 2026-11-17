# BrainHockey – LAN 2-player + power-ups

## How it works

* **Auto pairing.** Start the game on both PCs. Each one listens on the LAN for ~2.5–3.5 s (UDP broadcast, port 47777). If it finds a game, it joins. If not, it hosts one and broadcasts it once per second. If both PCs start hosting at the same moment, the one with the higher random session id gives up and joins the other.
* **Host-authoritative.** The host runs the ball, goals, scores and power-ups, and sends a snapshot 30×/s over TCP (port 47778). The client only sends its paddle position and power-up claims.
* **Fixed sides, same picture on both screens.** The host plays **blue on the left** (`Player`). The PC that joins plays **red on the right** (`Player2`). Each PC enables only its own side's BCI stimuli (via `BciSideSwitcher`). BCI trigger zones and paddle hits count only for the side played on that machine.
* **Power-up target (BCI class 4).** It appears at a random height on the centre line every 6–14 s and stays for 12 s. Both players can select it, and the host decides who was first. It gives one of these at random:

| Power-up | Effect | Duration |
|---|---|---|
| Turbo | Ball ×1.6 speed while in the **opponent's** half | 30 s |
| Slow-mo | Ball ×0.6 speed while in **your** half | 30 s |
| Big paddle | Your paddle ×1.6 taller | 30 s |
| Shrink | Opponent paddle ×0.6 | 30 s |
| Multi-ball | Ball splits in two | until the next goal |

Selecting the same power-up again restarts its timer. Effects from both players stack. All numbers can be changed in the inspector of `PowerUpManager`.

## Running the demo

* Put both PCs on the same network. A cheap router or phone hotspot is the safest choice: university and corporate Wi-Fi often **block broadcast and client-to-client traffic**.
* The first time you run it, allow the game through **Windows Firewall** for *Private networks* (UDP 47777, TCP 47778).
* Start the game on both PCs. The bottom-left status shows *Searching… → Hosting on 192.168.x.x… → Hosting – playing against …*.
* Either player presses **SPACE** to start. On the client, SPACE sends a request to the host.
* Test keys: **W/S** or **↑/↓** move your own paddle (blue on the host, red on the client). **E** grabs the power-up target. Offline, Right Shift grabs it for red.

Command-line overrides (handy for builds):

```
BrainHockey.exe -host             # always host
BrainHockey.exe -client           # only join (never host)
BrainHockey.exe -connect 192.168.1.20   # skip discovery, connect to this IP
BrainHockey.exe -offline          # original single-machine game
```

To test on one PC, run two builds. The second one joins via loopback and plays red. Whichever PC finds no game first becomes the host and plays blue. To decide who is blue, start the blue player's PC with `-host` and the other with `-client`.

## Notes

* If a player disconnects, both sides go back to 0-0. The host keeps hosting and the client searches again.
* Bug fix: `UpdatePlayerScore` never set `gameOver`, so scores did not reset after the left player won. It does now.
* Bounces now always send the ball *away* from the paddle or wall, instead of flipping the sign. This prevents double-flips with multi-ball and resized paddles.
* Snapshots use TCP with Nagle disabled. On a LAN the latency is a few ms, which is negligible next to BCI decision times.