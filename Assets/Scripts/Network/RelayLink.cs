// Pong — Homework 3 Step 5. Unity Relay: the host gets a join code, a guest joins with it. No IP address.
// Both sides talk only to Unity's Relay server, which forwards the packets (Week 6 Notes 6.1).
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Transporting.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public static class RelayLink
{
    /// How many guests one host can take (LEFT, RIGHT and a spectator besides the host).
    private const int MaxGuests = 3;

    /// Relay needs a signed-in player. Anonymous sign-in: no password, nothing in the repo.
    private static async Task SignInAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            InitializationOptions options = new InitializationOptions();
#if UNITY_EDITOR
            // Multiplayer Play Mode instances live under Library/VP/. Give them their own profile,
            // so the host and the guest on one PC are two different players (fix list).
            if (Application.dataPath.Contains("/Library/VP/")) options.SetProfile("guest");
#endif
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    /// Host: book a slot on a Relay server, get its join code, then start server + client through it.
    public static async Task<string> StartHostAsync(NetworkManager nm)
    {
        await SignInAsync();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxGuests);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        UnityTransport transport = nm.TransportManager.GetTransport<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));   // BEFORE StartConnection

        nm.ServerManager.StartConnection();
        nm.ClientManager.StartConnection();
        Debug.Log($"[Relay] Join code: {joinCode}");
        return joinCode;
    }

    /// Guest: turn the join code into the same Relay server's address, then start the client through it.
    public static async Task StartClientAsync(NetworkManager nm, string joinCode)
    {
        await SignInAsync();

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpperInvariant());

        UnityTransport transport = nm.TransportManager.GetTransport<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));   // BEFORE StartConnection

        nm.ClientManager.StartConnection();
        Debug.Log($"[Relay] Joined with code {joinCode}");
    }
}
