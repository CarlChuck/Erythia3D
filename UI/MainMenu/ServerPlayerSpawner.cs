using Unity.Netcode;
using UnityEngine;

public class ServerPlayerSpawner : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("ServerPlayerSpawner: Awake() called.");
    }

    void Start()
    {
        Debug.Log("ServerPlayerSpawner: Start() called.");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("ServerPlayerSpawner: NetworkManager.Singleton is NULL at the start of Start(). Cannot subscribe to events.");
            return;
        }

        Debug.Log($"ServerPlayerSpawner Start: NetworkManager.Singleton found. IsServer: {NetworkManager.Singleton.IsServer}, IsHost: {NetworkManager.Singleton.IsHost}");

        if (NetworkManager.Singleton.IsServer) // Simplified from IsServer || IsHost as dedicated server won't be host
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            Debug.Log("ServerPlayerSpawner: Successfully subscribed to OnClientConnectedCallback in Start (server was already active).");
        }
        else
        {
            Debug.Log("ServerPlayerSpawner: Server not yet active in Start. Subscribing to OnServerStarted.");
            NetworkManager.Singleton.OnServerStarted += HandleServerSuccessfullyStarted;
        }
    }

    private void HandleServerSuccessfullyStarted()
    {
        Debug.Log("ServerPlayerSpawner: OnServerStarted event received.");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            Debug.Log("ServerPlayerSpawner: Successfully subscribed to OnClientConnectedCallback via OnServerStarted.");
        }
        else
        {
            Debug.LogError("ServerPlayerSpawner: OnServerStarted event received, but NetworkManager is null or not a server!");
        }
        // It's good practice to unsubscribe from OnServerStarted once handled, if it's a one-time setup.
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnServerStarted -= HandleServerSuccessfullyStarted;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        Debug.Log($"ServerPlayerSpawner: Client connected with clientId: {clientId}. Attempting to spawn player object.");
        GameObject playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;

        if (playerPrefab != null)
        {
            GameObject playerInstance = Instantiate(playerPrefab);
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"ServerPlayerSpawner: Player object spawned for clientId: {clientId}");
            }
            else
            {
                Debug.LogError($"ServerPlayerSpawner: Player prefab '{playerPrefab.name}' does not have a NetworkObject component. Cannot spawn for clientId: {clientId}");
                Destroy(playerInstance);
            }
        }
        else
        {
            Debug.LogError("ServerPlayerSpawner: PlayerPrefab is not assigned in NetworkManager. Cannot spawn player object for clientId: " + clientId);
        }
    }

    private void OnDestroy()
    {
        Debug.Log("ServerPlayerSpawner: OnDestroy() called.");
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnServerStarted -= HandleServerSuccessfullyStarted; // Ensure this is also unsubscribed
        }
    }
}