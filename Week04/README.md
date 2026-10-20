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

