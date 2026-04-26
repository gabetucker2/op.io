#nullable enable

using System.Reflection;
using Microsoft.Xna.Framework;
using op.io;

internal static class Program
{
    private static int Main()
    {
        try
        {
            ResetRuntimeState();

            Console.WriteLine($"BaseDirectory={AppContext.BaseDirectory}");
            Console.WriteLine($"ProjectRoot={DatabaseConfig.ProjectRootPath}");
            Console.WriteLine($"DatabasePath={DatabaseConfig.DatabaseFilePath}");
            Console.WriteLine($"DatabaseExists={File.Exists(DatabaseConfig.DatabaseFilePath)}");

            Core core = CreateHeadlessCore();
            Core.Instance = core;
            core.GameObjects = new List<GameObject>();
            core.StaticObjects = new List<GameObject>();
            core.PhysicsManager = new PhysicsManager();

            List<Agent> agents = AgentLoader.LoadAgents();
            Console.WriteLine($"Loaded agents: {agents.Count}");
            foreach (Agent loadedAgent in agents)
            {
                Console.WriteLine($"  agent id={loadedAgent.ID} name={loadedAgent.Name} isPlayer={loadedAgent.IsPlayer} dynamic={loadedAgent.DynamicPhysics}");
            }
            foreach (Agent agent in agents)
            {
                ApplyDatabaseLoadout(agent);
            }

            List<GameObject> mapObjects = MapObjectLoader.LoadMapObjects();
            core.GameObjects.AddRange(mapObjects);
            core.StaticObjects.AddRange(mapObjects.Where(static go => !go.DynamicPhysics));
            core.GameObjects.AddRange(agents);

            Agent? player = agents.FirstOrDefault(static a => a.IsPlayer);
            Agent? scout = agents.FirstOrDefault(static a => string.Equals(a.Name, "ScoutSentry1", StringComparison.OrdinalIgnoreCase));
            if (player == null || scout == null)
            {
                Console.WriteLine("Simulation setup failed: player or ScoutSentry1 not found.");
                return 1;
            }

            core.Player = player;

            RunScenario("OpenLane", player, scout, includeWorldPhysics: false);
            RunScenario("WorldLoaded", player, scout, includeWorldPhysics: true);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Simulation failed: {ex}");
            return 1;
        }
    }

    private static void RunScenario(string name, Agent player, Agent scout, bool includeWorldPhysics)
    {
        ResetFrameState();

        List<GameObject> scenarioObjects = includeWorldPhysics
            ? Core.Instance.GameObjects
            : Core.Instance.GameObjects.Where(static go => go is Agent).ToList();

        Core.Instance.GameObjects = scenarioObjects;
        Core.Instance.StaticObjects = scenarioObjects.Where(static go => !go.DynamicPhysics).ToList();
        Core.Instance.Player = player;

        scout.Position = new Vector2(420f, 100f);
        scout.PreviousPosition = scout.Position;
        scout.PhysicsVelocity = Vector2.Zero;
        scout.MovementVelocity = Vector2.Zero;

        player.Position = scout.Position + new Vector2(-220f, 0f);
        player.PreviousPosition = player.Position;
        player.PhysicsVelocity = Vector2.Zero;
        player.MovementVelocity = Vector2.Zero;
        player.Rotation = 0f;

        Vector2 startScoutPosition = scout.Position;
        BulletManager.SpawnBullet(player);

        Console.WriteLine($"Scenario {name}");
        Console.WriteLine($"  scout dynamic={scout.DynamicPhysics} collidable={scout.IsCollidable} mass={scout.Mass:0.###} bodyMass={scout.BodyAttributes.Mass:0.###}");

        bool knockbackObserved = false;
        bool contactObserved = false;
        float maxPhysicsVelocity = 0f;
        float maxDisplacement = 0f;

        for (int frame = 0; frame < 240; frame++)
        {
            StepFrame();

            float displacement = Vector2.Distance(scout.Position, startScoutPosition);
            float physicsSpeed = scout.PhysicsVelocity.Length();
            maxPhysicsVelocity = MathF.Max(maxPhysicsVelocity, physicsSpeed);
            maxDisplacement = MathF.Max(maxDisplacement, displacement);

            IReadOnlyList<Bullet> bullets = BulletManager.GetBullets();
            Bullet? bullet = bullets.Count > 0 ? bullets[0] : null;
            if (bullet != null && Vector2.Distance(bullet.Position, scout.Position) <= ((bullet.Shape.Width + scout.Shape.Width) * 0.5f + 2f))
            {
                contactObserved = true;
            }

            if (!knockbackObserved && (physicsSpeed > 0.01f || displacement > 0.01f))
            {
                knockbackObserved = true;
                Console.WriteLine($"  first response frame={frame} scoutPos=({scout.Position.X:0.###},{scout.Position.Y:0.###}) scoutVel=({scout.PhysicsVelocity.X:0.###},{scout.PhysicsVelocity.Y:0.###})");
            }
        }

        Console.WriteLine($"  contactObserved={contactObserved}");
        Console.WriteLine($"  maxPhysicsVelocity={maxPhysicsVelocity:0.###}");
        Console.WriteLine($"  maxDisplacement={maxDisplacement:0.###}");
        Console.WriteLine();
    }

    private static void StepFrame()
    {
        const float dt = 1f / 60f;
        Core.DELTATIME = dt;
        Core.GAMETIME += dt;

        foreach (GameObject go in Core.Instance.GameObjects)
        {
            go.PreviousPosition = go.Position;
        }

        foreach (GameObject go in Core.Instance.GameObjects)
        {
            go.Update();
        }

        BulletManager.Update();
        BulletCollisionResolver.ResolveCollisions(BulletManager.GetBullets(), Core.Instance.GameObjects);
        BulletCollisionSystem.Update(dt);
        PhysicsManager.Update(Core.Instance.GameObjects);
    }

    private static void ApplyDatabaseLoadout(Agent agent)
    {
        List<Attributes_Barrel> barrels = BarrelLoader.LoadBarrelsForAgent(agent.ID);
        if (barrels.Count > 0)
        {
            agent.ClearBarrels();
            foreach (Attributes_Barrel barrel in barrels)
            {
                agent.AddBarrel(barrel);
            }
        }

        List<(string Name, Attributes_Body Attrs)> bodies = BodyLoader.LoadBodiesForAgent(agent.ID);
        if (bodies.Count > 0)
        {
            agent.ClearBodies();
            for (int i = 0; i < bodies.Count; i++)
            {
                agent.AddBody(bodies[i].Attrs);
                if (!string.IsNullOrWhiteSpace(bodies[i].Name))
                {
                    agent.Bodies[i].Name = bodies[i].Name;
                }
            }
        }
    }

    private static Core CreateHeadlessCore()
    {
        return new Core();
    }

    private static void ResetRuntimeState()
    {
        ResetFrameState();
        GameObjectRegister.ClearAllGameObjects();
    }

    private static void ResetFrameState()
    {
        Core.GAMETIME = 0f;
        Core.DELTATIME = 1f / 60f;
        ClearBulletManager();
        ClearBulletCollisionSystemContacts();
    }

    private static void ClearBulletManager()
    {
        FieldInfo bulletsField = typeof(BulletManager).GetField("_bullets", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo toRemoveField = typeof(BulletManager).GetField("_toRemove", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((IList<Bullet>)bulletsField.GetValue(null)!).Clear();
        ((HashSet<Bullet>)toRemoveField.GetValue(null)!).Clear();
    }

    private static void ClearBulletCollisionSystemContacts()
    {
        FieldInfo prevField = typeof(BulletCollisionSystem).GetField("_prevContacts", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo currField = typeof(BulletCollisionSystem).GetField("_currContacts", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((HashSet<long>)prevField.GetValue(null)!).Clear();
        ((HashSet<long>)currField.GetValue(null)!).Clear();
    }
}
