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

---

# Part B — The join code

## Step 4 — Packages and the transport

- **Multiplayer Services** (`com.unity.services.multiplayer`) from the Unity Registry, and **FishyUnityTransport** from `https://github.com/ooonush/FishyUnityTransport.git?path=Assets/FishNet/Plugins/FishyUnityTransport`.
- Project Settings ▸ Services: the project is linked to the cloud project `dgd0005-OnurKarakoyun`; Relay is enabled in the Unity Cloud dashboard (production environment).
- NetworkManager object: **UnityTransport** added, Connection Data ▸ Port = **7770**; TransportManager ▸ Transport = UnityTransport. Tugboat stays on the object, unused.
- After the install Unity asked for an editor restart (new Burst version); before the restart UnityTransport failed with `Burst failed to compile`.
- Tested: the old **Start Host** still works through UnityTransport — `Local server is started for UnityTransport`, `[Host]` line, a LEFT paddle. (UnityTransport numbers its clients from 1, so the host is client 1 now.)

## Step 5 — RelayLink.cs

`Assets/Scripts/Network/RelayLink.cs`: signs in anonymously (no password or key in the repo), then `StartHostAsync` (allocation → join code → `SetRelayServerData` → server + client) and `StartClientAsync` (join code → join allocation → `SetRelayServerData` → client).

- **What does `CreateAllocationAsync` return?** An `Allocation`: a reserved slot on one Unity Relay server for my game — its address and port, the allocation id and the keys the host uses to talk to that server.
- **What does `GetJoinCodeAsync` return?** A short string (six letters/digits, e.g. `GNBJFN`) that stands for that allocation, so a guest can find the same Relay server without knowing any IP.
- **Why is `SetRelayServerData` before `StartConnection`?** `StartConnection` opens the connection with whatever address the transport has at that moment; the Relay data must already be set, otherwise it connects to the old IP/port and the server stops at once.

## Step 6 — Two buttons and a code

`ConnectionHud`: fields `_joinCode`, `_relayCode`, `_relayBusy`; `async void HostViaRelay()` and `JoinViaRelay()`. When server and client are Stopped and no Relay call is running: **Host via Relay**, a text field for the code and **Join via Relay**. While the host is Started with a code: a big `JOIN CODE: …` label. Stop Host clears the code.

Tested (main editor = host, Player 2 = guest):

```
[Relay] Join code: GNBJFN
[Server] server Started
[Host] This process is the server AND a client. RTT is 0 ms.
[Server] Spawned paddle #3 for client 1: LEFT
--- guest, after typing GNBJFN ---
[Relay] Joined with code GNBJFN
[Client] client Started
[Client] I am client 2, playing RIGHT. My paddle is #4
```

One rally: LEFT served, RIGHT scored, **0 : 1** in both windows. Guest RTT through the Relay: **67–100 ms** (on `127.0.0.1` it was 0–33 ms).

![Guest window, joined through the Relay](guest-relay-step6.png)

## Step 7 — Your partner, from another network

_To do with the partner: they clone this repo, I Host via Relay and send the code, they Join via Relay from another network. Then swap._

| | |
|---|---|
| Partner's name | _fill in_ |
| Who hosted, who joined | _fill in_ |
| The two networks | _fill in_ |
| Guest RTT through the Relay | _fill in_ |
| Guest RTT on 127.0.0.1 (Step 6) | 0–33 ms |
| First try? If not, which fix line | _fill in_ |
