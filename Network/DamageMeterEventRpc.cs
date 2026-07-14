using Unity.NetCode;

/// <summary>
/// One combat event (a single HealthChange), broadcast from the server to every client.
/// Entities can't be sent directly across the network boundary, so victim/source are
/// identified by their (ghostId, spawnTick) pair, which SpawnedGhostEntityMap can resolve
/// back to a local Entity on each client - see DamageMeterReceiveSystem.
/// </summary>
public struct DamageMeterEventRpc : IRpcCommand
{
    public int VictimGhostId;
    public NetworkTick VictimSpawnTick;

    // SourceGhostId == 0 means no attributable source (environmental damage, etc).
    public int SourceGhostId;
    public NetworkTick SourceSpawnTick;

    // Negative = damage, positive = healing - matches HealthChange.amount directly.
    public int Amount;

    public bool WasKilled;
    public bool VictimIsBoss;
    public bool VictimIsPlayer;
    public bool SourceIsPlayer;
}
