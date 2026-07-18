using System.Text;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// Runs on every connected client (including the host's own client world) and turns
/// each broadcast DamageMeterEventRpc back into a named combat-log entry.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(RunSimulationSystemGroup))]
public partial class DamageMeterReceiveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        bool hasGhostMap = SystemAPI.HasSingleton<SpawnedGhostEntityMap>();
        NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly ghostMap = hasGhostMap
            ? SystemAPI.GetSingleton<SpawnedGhostEntityMap>().Value
            : default;

        var playerGhostLookup = GetComponentLookup<PlayerGhost>(true);
        var objectDataLookup = GetComponentLookup<ObjectDataCD>(true);
        EntityManager em = EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Mirrors the official RpcExample's PongSystem pattern (classic Entities.ForEach
        // lambda job, WithoutBurst + Run) rather than SystemAPI.Query, since that's the
        // proven-compiling shape for RPC receipt in this exact SDK version.
        Entities.WithAll<ReceiveRpcCommandRequest>().ForEach((Entity entity, in DamageMeterEventRpc rpc) =>
        {
            string victimName = ResolveName(
                rpc.VictimGhostId, rpc.VictimSpawnTick, hasGhostMap, ghostMap, playerGhostLookup, objectDataLookup, em);

            string sourceName = rpc.SourceGhostId == 0
                ? "Environment"
                : ResolveName(rpc.SourceGhostId, rpc.SourceSpawnTick, hasGhostMap, ghostMap, playerGhostLookup, objectDataLookup, em);

            CombatLogAggregator.Instance.RecordEvent(
                sourceName, victimName, rpc.Amount, rpc.WasKilled, rpc.VictimIsBoss, rpc.SourceIsPlayer, rpc.VictimIsPlayer);

            ecb.DestroyEntity(entity);
        }).WithoutBurst().Run();

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private static string ResolveName(
        int ghostId,
        NetworkTick spawnTick,
        bool hasGhostMap,
        NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly ghostMap,
        ComponentLookup<PlayerGhost> playerGhostLookup,
        ComponentLookup<ObjectDataCD> objectDataLookup,
        EntityManager em)
    {
        if (!hasGhostMap || !ghostMap.TryGetValue(new SpawnedGhost(ghostId, spawnTick), out Entity entity) || !em.Exists(entity))
        {
            return "Unknown";
        }

        if (playerGhostLookup.HasComponent(entity))
        {
            // PlayerController (the graphical/hybrid side of a player ghost) exposes the
            // actual chosen character name via its `playerName` property - same string
            // the game itself renders in nameplates. Fall back to a stable per-player
            // label only if that lookup fails for some reason.
            GameObject graphical = API.Client.GetGraphicalGameObject(entity);
            string realName = graphical != null ? graphical.GetComponent<PlayerController>()?.playerName : null;
            if (!string.IsNullOrEmpty(realName))
            {
                return realName;
            }

            return $"Player {playerGhostLookup[entity].playerIndex + 1}";
        }

        if (objectDataLookup.HasComponent(entity))
        {
            return Prettify(objectDataLookup[entity].objectID.ToString());
        }

        return "Unknown";
    }

    private static string Prettify(string rawEnumName)
    {
        var sb = new StringBuilder(rawEnumName.Length + 8);
        for (int i = 0; i < rawEnumName.Length; i++)
        {
            char c = rawEnumName[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(rawEnumName[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
