using System;
using System.Collections.Generic;
using MoonCleaners;
using MoonWorldEdit.Core;
using UnityEngine;
using Num = System.Numerics;
using Pose = MoonWorldEdit.Core.Pose;

namespace MoonWorldEdit.Plugin;

// The game side of WorldEdit: reading placed structures and belts, and adding or removing them the way the game's
// own build code does after its checks (no checks, no charge). See docs/design.md, "Research findings".
internal sealed class GameWorld
{
    private const int StructureCap = 9999, BeltCap = 20000; // the game's own limits
    private const float AimReach = 80f; // metres
    private const int AimMask = ~(1 << 2); // the builder's mask: everything but Ignore Raycast
    private const string FallbackOwner = "moon-base";

    private GameWorld(MoonWorld world, MoonDepotBuilder builder, MoonDepotConstruction construction)
    {
        World = world;
        Builder = builder;
        Construction = construction;
    }

    public MoonWorld World { get; }
    public MoonDepotBuilder Builder { get; }
    public MoonDepotConstruction Construction { get; }
    public Num.Vector3 Centre => ToNum(World.transform.position);

    private static GameWorld? cached;
    private static float nextLook;

    /// <summary>The loaded world, or null in the menus. Looked up at most twice a second while missing
    /// (FindObjectOfType is stripped from this game; FindAnyObjectByType is not).</summary>
    public static GameWorld? Find()
    {
        if (cached != null && cached.World != null && cached.Builder != null && cached.Construction != null)
            return cached;
        cached = null;
        if (Time.unscaledTime < nextLook)
            return null;
        nextLook = Time.unscaledTime + 0.5f;
        var world = UnityEngine.Object.FindAnyObjectByType<MoonWorld>();
        var builder = UnityEngine.Object.FindAnyObjectByType<MoonDepotBuilder>();
        if (world == null || builder == null)
            return null;
        var construction = world.GetComponent<MoonDepotConstruction>();
        cached = construction == null ? null : new GameWorld(world, builder, construction);
        if (cached != null)
            Plugin.Logger.LogInfo($"WorldEdit ready: {world.depots.Count} structures, {world.conveyors.Count} belts; " +
                $"crew {(MoonCrew.Joined ? "joined" : "solo")}, next structure id {world.nextDepotId}");
        return cached;
    }

    public static Num.Vector3 ToNum(Vector3 v) => new(v.x, v.y, v.z);
    public static Vector3 ToUnity(Num.Vector3 v) => new(v.X, v.Y, v.Z);
    private static Num.Quaternion ToNum(Quaternion q) => new(q.x, q.y, q.z, q.w);
    private static Quaternion ToUnity(Num.Quaternion q) => new(q.X, q.Y, q.Z, q.W);

    /// <summary>What the player is looking at: the hit point and the structure hit, if any.</summary>
    public bool Aim(out Vector3 point, out int structure, out Ray ray)
    {
        ray = Builder.ReadInput().aim;
        structure = 0;
        point = default;
        if (!Physics.Raycast(ray, out var hit, AimReach, AimMask, QueryTriggerInteraction.Ignore))
            return false;
        point = hit.point;
        structure = Construction.StructureAt(hit.collider);
        return true;
    }

    // ---- reading ----------------------------------------------------------------------------------------------------

    public List<WorldPiece> Pieces()
    {
        var list = new List<WorldPiece>();
        foreach (var entry in World.depots)
        {
            var rec = entry.Value;
            list.Add(new WorldPiece(rec.id, rec.kind, PoseOf(rec), rec.placedPose, rec.style, rec.support));
        }
        return list;
    }

    public List<WorldBelt> Belts()
    {
        var origin = World.transform.position;
        var list = new List<WorldBelt>();
        foreach (var entry in World.conveyors)
        {
            var rec = entry.Value;
            list.Add(new WorldBelt(ToNum(rec.start + origin), ToNum(rec.startDirection), ToNum(rec.end + origin), ToNum(rec.endDirection)));
        }
        return list;
    }

    // Structural pieces keep their rotation; machines keep a yaw that the game turns into one with the ground's tilt.
    private Pose PoseOf(MoonDepotSnapshot rec)
    {
        var local = new Vector3(rec.x, rec.y, rec.z);
        var position = local + World.transform.position;
        if (rec.placedPose)
            return new Pose(ToNum(position), Num.Quaternion.Normalize(new Num.Quaternion(rec.qx, rec.qy, rec.qz, rec.qw)));
        World.DepotPose(local.normalized, rec.yaw, out _, out var rotation, rec.kind);
        return new Pose(ToNum(position), ToNum(rotation));
    }

    // ---- writing ----------------------------------------------------------------------------------------------------

    /// <summary>The pose /paste snaps its anchor piece to at the aim, from the game's own planner (or the bare ground).</summary>
    public Pose SnapPose(int kind, Vector3 aim, int target, out string how)
    {
        var plan = World.PlanStructure(kind, aim, 0f, target, false, 0f, 0);
        if (plan != null && plan.snapped)
        {
            how = string.IsNullOrEmpty(plan.hint) ? "snapped" : plan.hint;
            return new Pose(ToNum(plan.position), ToNum(plan.rotation));
        }
        World.DepotPose((aim - World.transform.position).normalized, 0f, out var position, out var rotation, kind);
        how = "free (no snap here)";
        return new Pose(ToNum(position), ToNum(rotation));
    }

    /// <summary>Starts the crew diff before anything is added, so pasted pieces are sent (never call it after).</summary>
    public void PrepareCrewSync()
    {
        if (MoonCrew.Joined && !World.crewTracking)
            World.BeginCrewTracking();
    }

    public bool HasRoom(int pieces, int belts) =>
        World.depots.Count + pieces <= StructureCap && World.conveyors.Count + belts <= BeltCap;

    /// <summary>A free structure id: the game's next one, past any taken.</summary>
    public int TakeStructureId()
    {
        var id = World.nextDepotId;
        while (World.depots.ContainsKey(id))
            id++;
        World.nextDepotId = id + 1;
        return id;
    }

    private int TakeBeltId()
    {
        var id = Math.Max(1, World.nextConveyorId);
        while (World.conveyors.ContainsKey(id))
            id++;
        World.nextConveyorId = id + 1;
        return id;
    }

    /// <summary>Adds a structure like TryBuildStructure does after its checks. False if the game wouldn't spawn it.</summary>
    public bool Add(PlacedPiece piece)
    {
        var origin = World.transform.position;
        var position = ToUnity(piece.Pose.Position);
        var rec = new MoonDepotSnapshot
        {
            id = piece.Id,
            kind = piece.Kind,
            owner = string.IsNullOrWhiteSpace(MoonCrew.LocalPlayerId) ? FallbackOwner : MoonCrew.LocalPlayerId,
            x = position.x - origin.x,
            y = position.y - origin.y,
            z = position.z - origin.z,
            placedPose = piece.PlacedPose,
            support = piece.Support != 0 && World.depots.ContainsKey(piece.Support) ? piece.Support : 0,
            style = World.HasFoundationStyle(piece.Kind, piece.Style) ? piece.Style : 0,
            contributions = new Il2CppSystem.Collections.Generic.List<MoonWalletSnapshot>(),
        };
        var rotation = Num.Quaternion.Normalize(piece.Pose.Rotation);
        if (piece.PlacedPose)
        {
            rec.qx = rotation.X;
            rec.qy = rotation.Y;
            rec.qz = rotation.Z;
            rec.qw = rotation.W;
        }
        else
            rec.yaw = MachineYaw(piece.Kind, (position - origin).normalized, rotation);

        if (!Construction.Spawn(rec))
            return false;
        World.depots.Add(rec.id, rec);
        World.RefreshBeltsAt(World.PortPoints(rec));
        World.PressDustUnder(rec);
        var view = Construction.ViewOf(rec.id);
        if (view != null)
            World.RevealBuilt(view, 0);
        return true;
    }

    private float MachineYaw(int kind, Vector3 up, Num.Quaternion wanted)
    {
        World.DepotPose(up, 0f, out _, out var at0, kind);
        World.DepotPose(up, 90f, out _, out var at90, kind);
        return YawSolver.Solve(ToNum(at0 * Vector3.forward), ToNum(at90 * Vector3.forward),
            Num.Vector3.Transform(Num.Vector3.UnitZ, wanted), ToNum(up));
    }

    public int Add(WorldBelt belt)
    {
        var origin = World.transform.position;
        var rec = new MoonConveyorSnapshot
        {
            id = TakeBeltId(),
            owner = string.IsNullOrWhiteSpace(MoonCrew.LocalPlayerId) ? FallbackOwner : MoonCrew.LocalPlayerId,
            start = ToUnity(belt.Start) - origin,
            startDirection = ToUnity(Num.Vector3.Normalize(belt.StartDirection)),
            end = ToUnity(belt.End) - origin,
            endDirection = ToUnity(Num.Vector3.Normalize(belt.EndDirection)),
        };
        World.conveyors.Add(rec.id, rec);
        World.ConveyorViews.Built(rec);
        var view = World.ConveyorViews.ViewOf(rec.id);
        if (view != null)
            World.RevealBuilt(view, rec.id);
        return rec.id;
    }

    /// <summary>Removes a structure the way the game does, without a refund (nothing was paid).</summary>
    public void RemoveStructure(int id)
    {
        if (World.depots.ContainsKey(id))
            World.RemoveStructureNow(id);
    }

    public void RemoveBelt(int id)
    {
        if (!World.conveyors.TryGetValue(id, out var rec))
            return;
        World.conveyors.Remove(id);
        var view = World.ConveyorViews.Dismantled(rec, out var mesh);
        if (view != null)
            World.DismantleView(view, mesh);
    }

    public bool HasStructure(int id) => World.depots.ContainsKey(id);

    public void Save()
    {
        World.progressDirty = true;
        World.SaveProgress();
    }
}
