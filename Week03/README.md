# Week 3 — Pong connects and replicates

Onur Karakoyun · DGD0005 Multiplayer Programming · Homework 1

---

## Checkpoint D (Step 4)

1. **Which component gives the `ObjectId`?**
   The `NetworkObject` on the Paddle prefab. It gives the paddle the same `ObjectId` on the server and on every client, so "paddle #N" means the same paddle everywhere.

2. **Which machine chose LEFT, and how did the client learn it?**
   The **server** chose it, in `PaddleController.OnStartServer()`: it looks through `PaddleController.All` and writes the first free side into `Side.Value`. `Side` is a `SyncVar<int>`, so its value travels to the client inside the spawn message; on the client `OnStartClient()` reads it, and `Side.OnChange` → `Place()` moves the paddle to `x = -Court.PaddleX` and paints it blue.

3. **Which line stops other clients moving your paddle?**
   The first line of `PaddleController.Update()`:
   ```csharp
   if (!IsOwner) return;
   ```
   Only the client whose connection owns the paddle reads the keyboard; on every other machine the method returns immediately.

---

## Predictions (written before Step 6)

1. **You press W, nothing moves elsewhere. Who must tell whom, in what order?**
   My client (the owner) must tell the server the new paddle position, then the server must pass it on to every other client (observer): owner → server → others.

2. **Three balls. Which machine should own the one real ball? What must a client receive to draw it?**
   The server should simulate the one real ball (nobody's client owns it); a client only needs to receive the ball's position every tick to draw it.

3. **If the server believes "my paddle is at z = 3.2" from a client, what is the first cheat?**
   A client can send any position it likes — teleport the paddle straight to wherever the ball is (or move it faster than allowed), and the server will accept it.

---

## Connection card (Step 11)

| | Direction 1 (Onur = server) | Direction 2 (Mert = server) |
|---|---|---|
| Partner | Mert Çiçek (build .exe) | Onur (build .exe) |
| Network | Radmin VPN (different home internets) | Radmin VPN |
| Server IP | 26.78.90.12 (Onur) | 26.12.34.56 (Mert) |
| My ClientId + side | Onur: client 0, LEFT  |  Onur: client 1, RIGHT |
| RTT | Mert: 48 ms | Onur: 52 ms |
| Worked first try? | No — server firewall blocked Unity on Public; allowed Private + Public, then it connected | Yes |

Lesson (Notes 11): "Starting → Stopped" with the right IP is almost always the **server's** firewall, not the code.

---

## Design sheet (before Step 7)

| Thing | State or event? | FishNet piece | On which object? | Who writes / calls? |
|---|---|---|---|---|
| Ball position | state | NetworkTransform (client auth off) | Ball | server |
| Ball in play? | state | SyncVar\<bool\> | Ball | server |
| Two scores | state | SyncVar\<int\> ×2 | GameState | server |
| Whose serve | state | SyncVar\<int\> | GameState | server |
| "Let me serve" | event (request) | ServerRpc | Paddle | owner client |
| "LEFT scored!" to all | event | ObserversRpc | Paddle (scorer) | server |
| "YOU SERVE" to one | event | TargetRpc | Paddle (conceder) | server |
| Event feed | state | SyncList\<string\> | GameState | server |

`BallSpawner` on the `Court` object creates the one ball: it instantiates the Ball prefab at `(0, Court.BallY, 0)` in `OnStartServer()` and calls `Spawn(...)`, so it only ever runs on the server, once.
The first line of the ball's `Update` is `if (!IsServerInitialized) return;` — only the server moves the ball; clients just receive its position.

---

## Step 8

1. **Which RPC for a hit sound on all clients? Which for "not your serve" to one?**
   A hit sound for everyone is an `[ObserversRpc]` (server → all observers). "Not your serve" to one player is a `[TargetRpc]` (server → that one connection).

2. **Where is the score, who may change it?**
   In two `SyncVar<int>`s, `LeftScore` and `RightScore`, on the `GameState` scene object. Only the server may change them, in `GameState.Goal()`; clients only mirror them.

3. **Ball bounced on the server but passed through on your screen: which paddle copy did the server use, why different?**
   The server used **its own copy** of my paddle — the position my `NetworkTransform` last delivered to it, a tick or two old. My screen shows my paddle where it is *now*, so the two copies disagree; the server's copy is the one that counts.

---

## Step 10 — Bytes and tick rate

Measured with Multiplayer Play Mode (server = Player 2, two clients playing, one rally), server `out` read after 10 s.

| Tick rate | Server out B/s (rally) | Ball in a client window | Other paddle |
|---|---|---|---|
| 10 | ≈ 510 (414–631); server in ≈ 290; each client in ≈ 250 | steps: stands still in up to 30 % of frames (e.g. 130 of 430), visibly jerky; RTT 40–100 ms | glides most of the time, freezes now and then (98 still frames in one second); late |
| 30 | ≈ 1,370 (1,344–1,415); server in ≈ 860; each client in ≈ 685 | smooth: 0 still frames during flight; RTT 7–33 ms | smooth |
| 60 | ≈ 2,800 (2,743–2,830); server in ≈ 1,695; each client in ≈ 1,400 | smooth: 0 still frames during flight; RTT ≈ 17 ms | smooth (a few still frames out of ~500) |

- **What did 60 cost?** About twice the server's outgoing bytes per second compared with 30 — every moving object is sent twice as often to every client.
- **What did it buy?** Positions every ~17 ms instead of ~33 ms, so the ball and the other paddle are smoother and a little less behind.
- **Why did the ball step even for the player moving a paddle?** Nobody's client moves the ball: every client only draws the positions the server sends, and at 10 ticks they are 100 ms apart, too far for interpolation to hide. My own paddle stays smooth because I move it locally every frame.

Back at **30** for the hand-in.

### Grading my predictions

1. Owner → server → others — **right**: that is exactly the `NetworkTransform` path (Client Authoritative on).
2. Server simulates the one ball, clients receive its position — **right**: `NetworkBall.Update` starts with `if (!IsServerInitialized) return;` and a server-authoritative `NetworkTransform` ships the position.
3. Teleport the paddle to the ball — **right**: with Client Authoritative on, the server accepts whatever position the owner sends (Week 4 fixes this).

---

## Section 14 — Self-review answers

1. **No shared "now".** Light in fibre travels ~200,000 km/s; Istanbul → Frankfurt is ~1,900 km, so at least 9.5 ms one way (19 ms round trip) and 35–50 ms measured. A 60 fps frame is 16.7 ms, so the measured round trip is about 2–3 frames: whatever I see of the other player is already old.

2. **Topologies grid.**

   | | Dedicated server | Listen server | Peer-to-peer |
   |---|---|---|---|
   | Trust | the server process | the host player | nobody / everyone |
   | Cost | studio pays per server | host's upload | nothing |
   | Latency | everyone pings a neutral point | host 0, guests ping host | direct, if NAT allows |

   Pong used a **dedicated server**. In Step 5 the server was the Multiplayer Play Mode instance tagged `Server` (Player 2); in Step 11 it was the editor on the machine that pressed Start Server.

3. **UDP vs TCP.** TCP stops everything to resend one lost packet; for a game an old ball position is worthless, so UDP is used and reliability is added only where needed. Reliable: the score / "LEFT scored!". Unreliable: the ball position.

4. A `NetworkObject` gives an object an identity (`ObjectId`), an owner and network spawning. Inheriting `NetworkBehaviour` gives a script `IsOwner` / `IsServerInitialized`, the network callbacks, SyncVars and RPCs. The `Hud` has **neither**: it is local UI that only reads the `NetworkManager`; nothing about it is replicated.

5. **Same Paddle prefab on three machines (client 0 owns it):**

   | | Server | Client 0 (owner) | Client 1 |
   |---|---|---|---|
   | `IsOwner` | false | **true** | false |
   | `IsServerInitialized` | true | false | false |
   | Callback | `OnStartServer` | `OnStartClient` | `OnStartClient` |
   | Reads keyboard | no | **yes** | no |

6. The **`NetworkTransform`** made the difference. Path: owner's client → server → every other client. The setting is **Client Authoritative** (on for the paddle: the owner is trusted). On the Ball it is **off**, because the ball has no owner — the server simulates it and is the only one trusted.

7. `Court.Awake` is a plain Unity callback that runs on **every** process, so each instance built its own Local Ball. The spawner's `OnStartServer` runs only on the server, once, and `Spawn` replicates that one ball to all clients. If a client called `Spawn`, FishNet would refuse (only the server can spawn) and nobody else would see anything.

8. **State or event:**

   | Thing | Kind | Goes in |
   |---|---|---|
   | the score | state | SyncVar |
   | "LEFT scored" | event | ObserversRpc |
   | whose serve it is | state | SyncVar |
   | "you serve" | event | TargetRpc |
   | the sound of the ball hitting a paddle | event | ObserversRpc |
   | whether the ball is in play | state | SyncVar |
   | the ball's position | state | NetworkTransform |

9. The **server** decides, using **its own copies** of both paddles (as last received, a tick or two old) and its ball. The client whose paddle "clearly touched it" sees the ball go through (or bounce and snap back). If a client wrote the ball's `InPlay` itself, only its local copy would change — nobody else would see it and it would snap back at the next server send; and any client could serve whenever it liked.

10. 8 objects × 30 ticks/s × 40 bytes = **9,600 B/s ≈ 9.6 kB/s** received per client; with 4 clients the server sends 4 × 9.6 = **38.4 kB/s**. In my Step 10 table, tick 10 cut the traffic to roughly a third and made the ball step in every window; my own paddle did not change because I move it locally every frame — the network only carries it to the others.

## Vocabulary (notes Section 13, for the demo)

| Word | Meaning, in my words |
|---|---|
| topology | Who connects to whom and which machine is trusted. |
| dedicated server | A server process nobody plays on; every player is a client of it. |
| listen server | One player's game is also the server (the host); trusted, zero ping for them. |
| peer-to-peer | Players connect to each other directly; nobody is the referee. |
| client | A player's game: sends what the player wants, shows what the server says. |
| host | One process that is server and client at the same time. |
| connection | One client's link to the server. |
| ClientId | The small number FishNet gives each connection, starting at 0. |
| tick | One fixed simulation step of the server. |
| tick rate | Ticks per second (30 here); how often state is sent. |
| packet | One chunk of bytes on the network, a few hundred bytes, below the ~1,200 B MTU. |
| UDP vs. TCP | UDP throws packets (may be lost/late); TCP guarantees order and waits for lost bytes. Games use UDP. |
| reliable / unreliable | Reliable = guaranteed and ordered, costs latency (goals); unreliable = may be lost, always fresh (positions). |
| RTT / ping | Time for a message to go to the server and back; half is about the one-way delay. |
| bandwidth | Bytes per second sent/received. |
| NetworkManager | The one switchboard component that starts server/client and holds the sub-managers. |
| transport | The layer that moves the bytes (Tugboat = plain UDP); swappable without changing game code. |
| NetworkObject | Gives an object a network identity, an owner and spawning. |
| ObjectId | The id of a NetworkObject, the same on every machine. |
| NetworkBehaviour | Base class for scripts on networked objects: IsOwner, callbacks, SyncVars, RPCs. |
| scene object | A NetworkObject already in the scene; the server spawns it for every client, no owner. |
| prefab | A saved object template; networked prefabs are registered in DefaultPrefabObjects. |
| spawn | The server creating a networked object and telling every client to create its copy. |
| PlayerSpawner | Spawns the player prefab once per connection, owned by that connection. |
| owner | The client connection allowed to control an object (or nobody → server). |
| ownership | Who the owner is: decides IsOwner, who may call ServerRpcs, whose NetworkTransform sends. |
| observer | A client allowed to see an object and receive its updates. |
| replication | Copying state from the machine that knows it to the machines that need it. |
| state vs. event | State has a current value (score) → SyncVar; an event happens once (goal) → RPC. |
| SyncVar | A field only the server writes; every machine mirrors it, late joiners get it. |
| SyncList | A SyncVar for a whole list; sends operations, not the list. |
| OnChange | Callback that fires on every machine when a SyncVar changes. |
| NetworkTransform | Component that sends position (rotation, scale) every tick and interpolates it. |
| client-authoritative | The owner decides the value and the server relays it — convenient, cheatable. |
| interpolation | Drawing an object between the last received positions, a couple of ticks behind, so it glides. |
| ServerRpc | Method called on a client, runs on the server: a request. |
| ObserversRpc | Called on the server, runs on every observing client: an event for all. |
| TargetRpc | Called on the server, runs on one client: a private message. |
| RequireOwnership | ServerRpc option; `false` lets non-owners call it (FishNet passes the caller's connection). |
| serialization | Turning values into bytes to send, and back. |
| interest management | Sending each client only what is relevant to it (near, same scene/team). |
