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
