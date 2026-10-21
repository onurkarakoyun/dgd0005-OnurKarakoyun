# Week 4 — The server decides

Onur Karakoyun · DGD0005 Multiplayer Programming · Homework 2

Homework 1 Pong, with one change: the client sends only its **input** (up, down, nothing), the **server** moves the paddle, and the server ignores a second input from the same client inside **30 ms**.

---

## Step 2 — Input up, position down

The owner sends only its input (`ServerSetAxis(axis)`, every tick); the server moves the paddle in `OnTick` and the server-authoritative `NetworkTransform` sends the position back to everyone, **the owner included** (Send To Owner is now on). Pressing W, my paddle moves in every window, mine too, a little late.

RTT shown by the HUD (Multiplayer Play Mode, server and clients on one PC): **13 ms** — that is my delay between pressing W and seeing my own paddle move.

---

## Step 3 — Check the input

**The server receives `axis = 50`. Which line makes the paddle move at normal speed, and on which machine does that line run?**
`axis = Mathf.Clamp(axis, -1f, 1f);` in `ServerSetAxis` — it turns 50 into 1, so the paddle moves at `1 × speed`, the normal speed. It runs on the **server**: `ServerSetAxis` is a `[ServerRpc]`, so the client only calls it and its body runs on the server.

---

## Step 4 — No second input inside 30 ms

The server keeps one timestamp **per paddle** (`_lastInputTime`, an instance field), so one per client: a fast client cannot block the other player. A second input from the same paddle inside 30 ms is counted in `RejectedInputs`, logged as `[Server] Rejected input from client N: too fast.` and dropped.

- **Why 30 ms?** At tick rate 30 one tick is 33 ms, and an honest client sends exactly one input per tick. 30 ms is just under one tick: every normal input passes, anything faster than one tick cannot come from a keyboard, it is a program.
- **What happens with 5 ms?** As a rule it allows up to 200 inputs per second, about 6 per tick, so a cheat that spreads its inputs gets almost all of them through — nearly no protection. (In my code the server measures time in whole ticks, so gaps are 0 or 33 ms and 5 ms would still stop two inputs landing in the same tick; but the limit would no longer say "one input per tick".)
- **What happens with 500 ms?** The limit is 15 ticks long, so the server would throw away 14 of every 15 honest inputs: your own paddle would react only twice a second and stutter — the honest player is punished.

Observed (Multiplayer Play Mode, server and two clients as editor instances on one PC): normal play looks the same and both paddles move freely, so the timestamp really is per paddle. The counters grew by about 3 per second per paddle — more than "one or two a minute": the server measures time in whole ticks, and with three editors sharing one PC the clients' ticks drift against the server's, so two honest inputs land in the same server tick more often. Each one costs only a repeated axis value, so the paddle does not stutter.

---

## Step 5 — The cheating client

Debug keys in `PaddleController.Update`, after the owner guard: **C** calls `ServerSetAxis(1f)` 20 times in one frame, **X** calls `ServerSetAxis(50f)` once, **T** sets `transform.position` to the ball's position on the client, with no RPC.

| Key | Server console | Paddle (on the server) |
|---|---|---|
| C once | about 19 lines `[Server] Rejected input from client N: too fast.`, counter up by the spam (plus the few honest rejections from Step 4) | moved 0.27 = exactly one tick at normal speed |
| X once | nothing | moved 0.27 = one tick at normal speed, never faster |
| T once | nothing | did not move; the ball and the other client never saw it. On my own screen the paddle jumps to the ball and snaps back within a tick, because the server re-sends its position every tick (`NetworkTransform.ForceSend()` in `OnTick`). |

Screenshot of the server Console after one **C**:

![Server console after C](server-console-after-C.png)

1. **C sends 20 inputs and the server accepts one. Why is the paddle not 20 times faster?** All 20 arrive inside one server tick, so the rate limit keeps the first and rejects the rest, and the server only moves the paddle once per tick by `_serverAxis × speed × TickDelta` — sending more inputs never adds more movement.
2. **T moved the paddle on your screen. Why did the ball on the server not notice?** The teleport only changed the client's own copy of the transform; no RPC was sent, and with Client Authoritative off the server never reads the client's position, so the server's paddle — the one the ball collides with — stayed where the server put it.
3. **Which of the three cheats would also fail without the rate limit, and why?** X and T: X is stopped by the clamp in Step 3 (50 becomes 1) and T by the server authority of Step 1–2 (the client's position is never sent); only C needs the rate limit.


---

## Vocabulary (Week 4, for the demo)

| Word | Meaning, in my words |
|---|---|
| server authority | The server decides where things are; clients only ask. |
| client authoritative | The owner's client writes the position and the server believes it (on in Week 3, **off** now). |
| input | What the player wants (up, down, nothing), not where the paddle is. |
| axis | The input as one number: +1 up, −1 down, 0 nothing. |
| ServerRpc | A method the owner client calls but whose body runs on the server (`ServerSetAxis`). |
| tick | One fixed server step (33 ms at tick rate 30); the owner sends one input per tick. |
| OnTick | FishNet event fired every tick; the owner sends input and the server moves the paddle there. |
| TickDelta | The length of one tick in seconds (1/30); used instead of `Time.deltaTime`. |
| validate | The server checks every input before using it: NaN, clamp, spectator. |
| NaN | "Not a number" — an invalid float; the server drops it. |
| clamp | Force a value into a range: `Mathf.Clamp(axis, -1, 1)` turns 50 into 1. |
| spectator | A connection with no side; its inputs are ignored. |
| rate limit | The server ignores a second input from the same client inside 30 ms. |
| timestamp | The time of the last accepted input, kept **per paddle** (`_lastInputTime`). |
| rejected input | An input the rate limit dropped; counted in `RejectedInputs` and logged. |
| cheat | A client sending what an honest keyboard can't: 20 inputs at once (C), axis 50 (X), a teleport (T). |
| NetworkTransform | Sends the server's paddle position to every client, the owner too (`Send To Owner`). |
| ForceSend | Makes the NetworkTransform send the position this tick even if it didn't change, so a teleport snaps back. |
| RTT | Round-trip time: input goes to the server, the new position comes back (13 ms here). |
