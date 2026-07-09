using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// Reads the game's own HealthChangeBuffer singleton - the single queue every damage/heal
/// in the game funnels through each tick - one tick before UpdateHealthFromBufferSystem
/// consumes and clears it. Runs server-only: HealthChangeBuffer is never replicated to
/// clients, so this is the only place the full, authoritative combat log exists.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UpdateHealthSystemGroup))]
[UpdateBefore(typeof(UpdateHealthFromBufferSystem))]
public partial class DamageMeterCaptureSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var changes = SystemAPI.GetSingletonBuffer<HealthChangeBuffer>(true);
        if (changes.Length == 0)
        {
            return;
        }

        var ghostLookup = GetComponentLookup<GhostInstance>(true);
        var ownerLookup = GetComponentLookup<OwnerReferenceCD>(true);
        var playerGhostLookup = GetComponentLookup<PlayerGhost>(true);
        var bossLookup = GetComponentLookup<BossCD>(true);
        var enemyLookup = GetComponentLookup<EnemyCD>(true);
        var objectDataLookup = GetComponentLookup<ObjectDataCD>(true);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < changes.Length; i++)
        {
            HealthChange change = changes[i].healthChange;
            if (change.amount == 0 || !ghostLookup.HasComponent(change.entity))
            {
                // No ghost on the victim means no stable id to send across the network - skip it.
                continue;
            }

            // HealthChangeBuffer carries every health change in the game (plants, tiles,
            // critters, structures...), not just combat. Keep the meter focused on
            // players/enemies/bosses rather than showing crop-harvesting as "damage".
            bool victimIsCombatRelevant = playerGhostLookup.HasComponent(change.entity)
                || enemyLookup.HasComponent(change.entity)
                || bossLookup.HasComponent(change.entity);
            if (!victimIsCombatRelevant)
            {
                continue;
            }

            Entity effectiveSource = ResolveEffectiveSource(change.causedByEntity, ownerLookup, playerGhostLookup);
            bool sourceHasGhost = effectiveSource != Entity.Null && ghostLookup.HasComponent(effectiveSource);

            // TEMP DIAGNOSTIC: only logs when causedByEntity actually has an OwnerReferenceCD
            // chain (i.e. some kind of owned entity - minion/pet/turret/projectile) that
            // still failed to resolve to a player. Plain wild-enemy-attacks-player hits
            // (no owner ref at all) are expected to end up here and aren't logged - they're
            // not a bug. Safe to remove once source attribution is confirmed solid.
            bool resolvedToPlayer = sourceHasGhost && playerGhostLookup.HasComponent(effectiveSource);
            bool causedByHasOwnerRef = change.causedByEntity != Entity.Null && ownerLookup.HasComponent(change.causedByEntity);
            if (causedByHasOwnerRef && !resolvedToPlayer)
            {
                Entity ownerRefTarget = ownerLookup[change.causedByEntity].owner;
                string causedByObjectId = objectDataLookup.HasComponent(change.causedByEntity)
                    ? objectDataLookup[change.causedByEntity].objectID.ToString()
                    : "n/a";
                string ownerObjectId = objectDataLookup.HasComponent(ownerRefTarget)
                    ? objectDataLookup[ownerRefTarget].objectID.ToString()
                    : "n/a";
                Debug.Log($"[CombatMeter DIAG] Owned entity did not resolve to a player: causedBy={change.causedByEntity} objectID={causedByObjectId} " +
                          $"owner={ownerRefTarget} ownerObjectID={ownerObjectId} ownerIsPlayerGhost={playerGhostLookup.HasComponent(ownerRefTarget)} " +
                          $"effectiveSource={effectiveSource} effectiveSourceHasGhost={sourceHasGhost}");
            }
            else if (resolvedToPlayer && effectiveSource != change.causedByEntity)
            {
                Debug.Log($"[CombatMeter DIAG] Resolved owned entity to player: causedBy={change.causedByEntity} -> player={effectiveSource}");
            }

            GhostInstance victimGhost = ghostLookup[change.entity];
            var rpc = new DamageMeterEventRpc
            {
                VictimGhostId = victimGhost.ghostId,
                VictimSpawnTick = victimGhost.spawnTick,
                Amount = change.amount,
                WasKilled = change.wasKilled,
                VictimIsBoss = bossLookup.HasComponent(change.entity),
                VictimIsPlayer = playerGhostLookup.HasComponent(change.entity),
                SourceIsPlayer = sourceHasGhost && playerGhostLookup.HasComponent(effectiveSource),
            };

            if (sourceHasGhost)
            {
                GhostInstance sourceGhost = ghostLookup[effectiveSource];
                rpc.SourceGhostId = sourceGhost.ghostId;
                rpc.SourceSpawnTick = sourceGhost.spawnTick;
            }

            Entity rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, rpc);
            ecb.AddComponent<SendRpcCommandRequest>(rpcEntity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Mirrors the kill-attribution walk in the game's own UpdateHealthFromBufferSystem:
    /// a hit from a minion/turret/projectile should be attributed to the player that owns it.
    /// </summary>
    private static Entity ResolveEffectiveSource(
        Entity causedByEntity,
        ComponentLookup<OwnerReferenceCD> ownerLookup,
        ComponentLookup<PlayerGhost> playerGhostLookup)
    {
        if (causedByEntity == Entity.Null)
        {
            return Entity.Null;
        }

        if (playerGhostLookup.HasComponent(causedByEntity))
        {
            return causedByEntity;
        }

        Entity owner = causedByEntity;
        for (int i = 0; i < 10 && ownerLookup.HasComponent(owner); i++)
        {
            owner = ownerLookup[owner].owner;
            if (playerGhostLookup.HasComponent(owner))
            {
                return owner;
            }
        }

        // No player found up the ownership chain - fall back to the raw source
        // (e.g. an enemy attacking a player directly).
        return causedByEntity;
    }
}
