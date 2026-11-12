# Week 5 — Pong on a host

Onur Karakoyun · DGD0005 Multiplayer Programming · In-class assignment (Listen server)

Homework 1 Pong, unchanged, now run on a **host**: one process is the server **and** a client. A guest joins, the host leaves, a partner joins over the LAN.

## Setup check

- Unity `6000.3.2f1`, FishNet 4.7.3R, Multiplayer Play Mode installed.
- `ServerManager ▸ Start On Headless` off, `NetworkManager ▸ Run In Background` on, tick rate 30, Tugboat port 7770.

---

## Steps 0–2 — a host

- **Step 0:** Start Server in the editor, Start Client in a Play Mode instance, one rally: no red lines, the score shows in the HUD.
- **Step 1:** main editor only, Start Server, then `127.0.0.1` + Start Client in the same window. Console:
  `[Server] server Started` → `[Client] client Starting` → `[Server] client 0 connected. Clients: 1` → `[Client] client Started` → `[Client] I am client 0, playing LEFT. My paddle is #3`. A LEFT paddle, W/S move it, the ball waits. HUD: `Server + Client`, `Clients: 1`, `ClientId: 0`.
- **RTT on the host:** my HUD shows **33 ms**, not 0 — that is exactly one tick at 30 Hz. FishNet answers the ping on the next tick; nothing travels over a network, the client talks to its own server in memory. Real network delay: 0.
- **Step 2:** `ConnectionHud` got **Start Host** (shown when server and client are both Stopped: `ServerManager.StartConnection()` then `ClientManager.StartConnection("127.0.0.1", port)`) and **Stop Host** (both Started: stop the client, then the server). `ConnectionLogger.OnClientConnectionState` prints `[Host] This process is the server AND a client. RTT is 0 ms.` when the client is Started and `ServerManager.Started` is true — checked in the client handler, because by then the server has started.
- **Start Server only:** only `[Server]` lines, the ball is spawned, no paddle, no keyboard. That is the dedicated server from Week 4 — same scripts.

## Step 3 — both callbacks, one paddle

On Start Host the Console shows, for the same object:

```
[Server] OnStartServer paddle #3: All.Count = 1
[Server] Spawned paddle #3 for client 0: LEFT
[Client] OnStartClient paddle #3: All.Count = 1
[Client] I am client 0, playing LEFT. My paddle is #3
```

Both `All.Add` calls (in `OnStartServer` and `OnStartClient`) are guarded with `if (!All.Contains(this))`. The HUD panel `Paddles in All:` says **1** on the host with one paddle, not 2.

- **Which callback ran first?** `OnStartServer`.
- **Which half of the host ran it?** The **server** half: it spawned the paddle and chose LEFT; then the client half of the same process received the spawn and ran `OnStartClient` on the **same** object — without the guard it would add itself a second time.

## Step 4 — a guest joins

Multiplayer Play Mode, Player 2 active, no tags. Main editor: Start Host. Player 2: Start Client on `127.0.0.1`.
The host logs `client 1 connected`, a RIGHT paddle appears, the guest's HUD shows `ClientId: 1` and an RTT above 0. The host serves (LEFT first), a rally, the score changes to **0 : 1** in both windows and the feed matches.

## Step 5 — the host wins

- On the **guest**, the ball sometimes goes into the paddle on screen, snaps back and bounces. On the **host** that never happens.
- The host's serve starts the instant Space is pressed; the guest's serve starts about one RTT later.
- **Why:** the server bounces the ball on its copy of each paddle. The guest's paddle on the server is the position from the guest's last input, a tick old. The host's paddle on the server *is* the host's paddle — no delay, no copy. That is host advantage.
- **Guest's RTT:** 8–33 ms (two editors on one PC; it jumps between under a tick and one tick).
- **Tick rate 10:** on the guest the ball moves in visible steps, about 100 ms apart, and the snap-backs at the paddle are bigger; the guest's own paddle reacts later because its input is only sent every 100 ms. On the host the ball looks the same steps but the bounce is always right. Back to **30**.

## Step 6 — the host leaves

- **Stop Host mid-rally:** the guest logs `[Client] client Stopping`, `[Client] client Stopped`. Both paddles, the ball and the score are gone on the guest. Start Host + Start Client again: new ids (0 and 1), score **0 : 0**. Nothing exists unless the server says so — the server just went home.
- **The other direction:** the guest clicks Stop Client. The host logs `[Server] client 1 disconnected` and `feed: client 1 left (RIGHT)`, the RIGHT paddle is gone, the host keeps playing (`Clients: 1`, `Paddles in All: 1`). Only one machine is special.

## Step 7 — Connection card

| | Direction 1 (I host) | Direction 2 (partner hosts) |
|---|---|---|
| Partner | Mert Çiçek | Mert Çiçek |
| Network | classroom LAN, cable | classroom LAN, cable |
| Host's address | 192.168.1.7 (my desktop) | 192.168.1.12 (Mert's laptop) |
| My ClientId + side | 0, LEFT (host) | 1, RIGHT (guest) |
| My RTT | 33 ms on the HUD = one tick, tells nothing | 4 ms |
| Mert's RTT | 3 ms | 33 ms (he is the host) |
| Connected first try? | **Yes** — my firewall was already fixed in Week 3. | **No.** Starting → Stopped after ~10 s. Fix line 2: the host's firewall — Mert allowed Unity for Private + Public, then it connected on the first retry. |

Three rallies each way. The guest's paddle moved on both machines; the host saw `client 1 connected`.

## Step 8 — the same scripts, three ways

1. **Start Server only:** `ServerBootstrap`/HUD, `ConnectionLogger` (server lines), `BallSpawner.OnStartServer`, `NetworkBall` (moves the ball), `GameState` (writes score and feed), `PaddleController.OnStartServer` + server tick. Not run: `OnStartClient`, keyboard reading, `Place()` on a client, `AnnounceGoal` / `YouServe` effects — there is no client and no paddle until someone joins.
2. **Start Host:** every networked script runs **both** halves on the same object: `PaddleController` runs `OnStartServer` **and** `OnStartClient` (hence the guard), `ConnectionLogger` prints `[Server]` **and** `[Client]` lines, `GameState` writes the feed as server and shows it as client, `ObserversRpc` effects (goal flash) run on the host as a client too.
3. **Start Client (guest):** it knows only an IP and a port. It does not know the server is a player — the host's paddle is just another paddle with `IsOwner = false`, exactly like in Week 3.

---

## Self-review

1. **Start Host: which two calls? Why is the host's RTT 0?**
   `_nm.ServerManager.StartConnection();` and `_nm.ClientManager.StartConnection("127.0.0.1", port);`. The client talks to a server in the same process, through memory — no cable, no network delay (my HUD shows 33 ms, which is only the one-tick wait for the ping answer).

2. **The guest's paddle on the host, the guest and a spectator:**

   | | Host | Guest (owner) | Spectator |
   |---|---|---|---|
   | `IsOwner` | false | **true** | false |
   | `IsServerInitialized` | **true** | false | false |
   | Callbacks | `OnStartServer` + `OnStartClient` | `OnStartClient` | `OnStartClient` |
   | Reads the keyboard | no | **yes** | no |

3. **Trace both serves.**
   Host: Space → `RequestServe()` (a ServerRpc, but the server is in the same process) → server checks side + not in play → `ball.Serve()` → ball moves. No network.
   Guest: Space → `RequestServe()` sent over UDP to the host → server checks → `ball.Serve()` → the new ball position travels back to the guest → ball moves on the guest's screen. **The guest's serve waits**, about one RTT.

4. **Step 6: why did the guest's own paddle vanish? Why did the host survive?**
   The guest's paddle was spawned by the server and only exists because the server says so; when the host stopped, the server went away and the client destroyed every networked object, its own paddle too. When the guest stopped, only one connection left the server: the server despawned that one paddle and kept running — the host is the server.

5. **State or event:**

   | Thing | Kind |
   |---|---|
   | the score | state (SyncVar) |
   | "RIGHT scored!" | event (ObserversRpc) |
   | whose serve | state (SyncVar) |
   | "YOU SERVE" | event (TargetRpc) |
   | the ball's position | state (NetworkTransform) |
   | the feed | state (SyncList) |

6. **A guest writes `GameState.Instance.LeftScore.Value = 9`.**
   Only the guest's local copy changes (FishNet warns that a client cannot write a server SyncVar); nobody else sees it and the next server value overwrites it. On the host the same line runs on the **server** half, which owns the SyncVar, so it really changes the score for everyone — that is why "works on the host" is not a test; test from a guest.

7. **The feed logs twice on the host.** The goal sound/flash belongs behind `if (!asServer)` in the `OnChange` handler (or in the `ObserversRpc`, which runs once per client): on the host the server-side call is skipped, so it plays once. The score log does not double because `GameState.Goal()` runs only on the server (`if (!IsServerInitialized) return;`) and logs once; the client half just mirrors the SyncVar without logging.
