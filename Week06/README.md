# Week 6 — The host cheats, and the join code

Onur Karakoyun · DGD0005 Multiplayer Programming · Homework 3

Homework 2 Pong on a host. Part A: a cheating guest fails, a cheating host wins. Part B: Unity Relay — the host gets a six-letter join code, the partner joins from another network, no IP address.

In every step the main editor clicks **Start Host**; the Multiplayer Play Mode instance (Player 2, no tag) is the guest.

---

# Part A — Who can cheat

## Step 1 — The guest cheats

New debug key **G** in `PaddleController.Update`, next to C, X, T: `GameState.Instance.LeftScore.Value = 9;` — no RPC, a direct write.

| Key, on the guest | Host Console | What both windows show |
|---|---|---|
| C | 19 lines `[Server] Rejected input from client 1: too fast.` (plus the few honest ones from Week 4) | the paddle moves 0.27 = one tick, normal speed |
| X | nothing | normal speed, never faster |
| T | nothing | jumps to the ball in the guest window only, snaps back within a tick |
| G | nothing | the score stays **0 : 0** on the host and on the guest |

**G wrote a SyncVar on the guest. Why did the host never see it?** A SyncVar belongs to the server: on the guest FishNet refused the write (`Cannot complete operation as server when server is not active.`), so the value did not even change on the guest and nothing was sent — only the server's writes travel to the clients.

## Step 2 — One more check: the serve

The guest could press Space 100 times a second. Now the server accepts one serve request per second **per player**:

- `private const double MinServeGap = 1.0;` and `private double _lastServeTime = -1;` — instance fields, not static, so one player's timestamp never blocks the other.
- In `ServerRequestServe`, after the null check and before the `ServingSide` check: if the last request was less than 1 s ago → `TargetRefused(Owner, "TOO FAST")` and return.
- `[TargetRpc] TargetRefused(NetworkConnection target, string why)` flashes the reason on that one player's HUD.
- Order: well-formed? → too fast? → allowed by the rule? → write state → announce.

Tested: two fast Space presses in the guest window → the guest's HUD flashed **TOO FAST**, the host's HUD did not. One second later Space works again.

## Step 3 — The host cheats

Same keys, pressed in the **host** window, then Speed 8 → 40 on the host's paddle in the Inspector.

| On the host | Result | Why |
|---|---|---|
| C | 19 rejected, normal speed (0.27 per tick) | it is a request; the check runs for the host too |
| X | normal speed | it is a request; the clamp runs |
| T | the paddle jumps to the ball and **stays**, in both windows | a direct write; on the host that is the server's position |
| G | LEFT has **9**, in both windows | a direct write; on the host that is the server's score |
| Speed 40 | the host's paddle moves 1.33 per tick instead of 0.27 — five times faster, in both windows | the host's numbers are the rules |

Guest window after the host pressed G:

![Guest window after the host pressed G](guest-after-host-G.png)

1. **C and X failed on the host, T and G worked. What is the difference?** C and X are **requests** (`ServerSetAxis` RPCs) that go through the server's checks even on the host, while T and G are **direct writes** to the transform and the SyncVar — on the host those are the server's own objects, so nothing checks them.
2. **Which of the host's cheats could a guest see?** All the ones that worked: the paddle on the ball (T), the 9 on the scoreboard (G) and the paddle five times faster (Speed 40) — the guest sees them, but cannot stop them.
3. **Ranked game with strangers: host or dedicated server?** A dedicated server, because on a host one of the players **is** the server and can change any rule, score or position, and nobody can check them.
