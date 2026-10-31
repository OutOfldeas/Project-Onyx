using System;
using System.Linq;
using NUnit.Framework;
using Onyx.Core.CrashSite;
using Onyx.Core.Persistence;
using Onyx.Core.Session;
using Onyx.Core.Skills;

namespace Onyx.Core.Tests.Persistence
{
    public class WorldPersistenceTests
    {
        private static WorldState ChangedWorld()
        {
            var world = WorldState.CreateNew(4711);
            world.Clock.Restore(0.62f, 3);

            var o2 = world.CrashSite.Point(StationKind.OxygenGenerator);
            world.CrashSite.ApplySyncedProgress(o2.Index, o2.Work);
            var reactor = world.CrashSite.Point(StationKind.Reactor);
            world.CrashSite.ApplySyncedProgress(reactor.Index, reactor.Work * 0.4f);

            world.Ship.RemoveModuleAt(world.Ship.Modules[0].X, world.Ship.Modules[0].Y);

            var neighbor = world.Galaxy.LanesOf(world.Navigation.CurrentSystemId).First().Other(world.Navigation.CurrentSystemId);
            world.Navigation.DebugSetSystem(neighbor);
            world.Navigation.Fuel.Drain(5f);
            return world;
        }

        [Test]
        public void RoundTrip_KeepsEverythingPlayersChanged()
        {
            var original = ChangedWorld();

            var data = WorldPersistence.Capture(original, 123f);
            var restored = WorldPersistence.Restore(data);

            Assert.That(restored.Seed, Is.EqualTo(original.Seed));
            Assert.That(restored.Clock.TimeOfDay, Is.EqualTo(original.Clock.TimeOfDay).Within(1e-5f));
            Assert.That(restored.Clock.DayCount, Is.EqualTo(3));

            for (int i = 0; i < original.CrashSite.Layout.RepairPoints.Count; i++)
            {
                Assert.That(restored.CrashSite.Job(i).Progress, Is.EqualTo(original.CrashSite.Job(i).Progress).Within(1e-4f));
            }

            Assert.That(restored.Ship.Modules.Count, Is.EqualTo(original.Ship.Modules.Count));
            Assert.That(restored.Ship.HullCount, Is.EqualTo(original.Ship.HullCount));

            Assert.That(restored.Navigation.CurrentSystemId, Is.EqualTo(original.Navigation.CurrentSystemId));
            Assert.That(restored.Navigation.Fuel.Current, Is.EqualTo(original.Navigation.Fuel.Current).Within(1e-4f));
            Assert.That(restored.Galaxy.Systems.Where(s => s.Discovered).Select(s => s.Id),
                Is.EquivalentTo(original.Galaxy.Systems.Where(s => s.Discovered).Select(s => s.Id)));

            Assert.That(data.PlayTimeSeconds, Is.EqualTo(123f));
        }

        [Test]
        public void RepairedEngine_RestoresJumpRange()
        {
            var world = WorldState.CreateNew(8);
            foreach (var point in world.CrashSite.Layout.RepairPoints)
            {
                world.CrashSite.ApplySyncedProgress(point.Index, point.Work);
            }

            var restored = WorldPersistence.Restore(WorldPersistence.Capture(world));

            Assert.That(restored.CrashSite.AllRepaired, Is.True);
            Assert.That(restored.Navigation.JumpRange, Is.EqualTo(world.Navigation.JumpRange).Within(1e-4f));
            Assert.That(restored.Navigation.JumpRange, Is.GreaterThan(0f));
        }

        [Test]
        public void FutureVersion_IsRejected()
        {
            var data = WorldPersistence.Capture(WorldState.CreateNew(1));
            data.Version = WorldPersistence.CurrentVersion + 1;

            Assert.Throws<SaveFormatException>(() => WorldPersistence.Restore(data));
        }

        [Test]
        public void MissingOptionalParts_StillLoads()
        {
            var data = new WorldSaveData { Version = 1, Seed = 99, CurrentSystemId = -1 };

            var world = WorldPersistence.Restore(data);

            Assert.That(world.Seed, Is.EqualTo(99));
            Assert.That(world.CrashSite.RepairedCount, Is.EqualTo(0));
        }

        [Test]
        public void UnknownModules_AreSkipped()
        {
            var data = WorldPersistence.Capture(WorldState.CreateNew(2));
            data.Ship.Modules = data.Ship.Modules.Concat(new[] { new ModuleSaveData { Id = "warpkern9000", X = 1, Y = 1 } }).ToArray();

            var world = WorldPersistence.Restore(data);

            Assert.That(world.Ship.Modules.Count, Is.EqualTo(data.Ship.Modules.Length - 1));
        }

        [Test]
        public void Character_RoundTrip()
        {
            var skills = new CharacterSkills(SkillCatalog.CreateDefault());
            skills.DebugSetLevel(Discipline.Engineering, 3);
            skills.AddExperience(Discipline.Engineering, 40f);
            skills.Unlock("eng.repair1");
            skills.SetLock(Discipline.Diplomacy, SkillLock.Lower);

            var restored = new CharacterSkills(SkillCatalog.CreateDefault());
            CharacterPersistence.Restore(restored, CharacterPersistence.Capture(skills));

            Assert.That(restored.Level(Discipline.Engineering), Is.EqualTo(3));
            Assert.That(restored.Experience(Discipline.Engineering), Is.EqualTo(skills.Experience(Discipline.Engineering)).Within(1e-4f));
            Assert.That(restored.IsUnlocked("eng.repair1"), Is.True);
            Assert.That(restored.GetLock(Discipline.Diplomacy), Is.EqualTo(SkillLock.Lower));
        }

        [Test]
        public void Character_UnknownSkills_AreSkipped()
        {
            var data = CharacterPersistence.Capture(new CharacterSkills(SkillCatalog.CreateDefault()));
            data.Unlocked = new[] { "gibt.es.nicht" };

            var restored = new CharacterSkills(SkillCatalog.CreateDefault());
            CharacterPersistence.Restore(restored, data);

            Assert.That(restored.Unlocked, Is.Empty);
        }

        [Test]
        public void Character_TravelsWithPlayerIntoNewWorld()
        {
            var first = WorldState.CreateNew(1);
            first.LocalSkills.DebugSetLevel(Discipline.Logistics, 4);

            var second = WorldState.CreateNew(2);
            second.AttachLocalCharacter(first.LocalSkills);

            Assert.That(second.LocalSkills.Level(Discipline.Logistics), Is.EqualTo(4));
        }

        [Test]
        public void Apply_UpdatesExistingWorldInPlace()
        {
            var host = ChangedWorld();
            var client = WorldState.CreateNew(host.Seed);
            var clientSite = client.CrashSite;

            WorldPersistence.Apply(client, WorldPersistence.Capture(host));

            Assert.That(client.CrashSite, Is.SameAs(clientSite), "Gelände bleibt dasselbe Objekt");
            Assert.That(client.Navigation.CurrentSystemId, Is.EqualTo(host.Navigation.CurrentSystemId));
            Assert.That(client.Ship.Modules.Count, Is.EqualTo(host.Ship.Modules.Count));
            Assert.That(client.CrashSite.IsRepaired(StationKind.OxygenGenerator), Is.True);
        }

        [Test]
        public void Apply_ForgetsSystemsTheHostHasNotSeen()
        {
            var host = WorldState.CreateNew(5);
            var client = WorldState.CreateNew(5);
            foreach (var system in client.Galaxy.Systems) system.Discovered = true;

            WorldPersistence.Apply(client, WorldPersistence.Capture(host));

            Assert.That(client.Galaxy.Systems.Count(s => s.Discovered), Is.EqualTo(host.Galaxy.Systems.Count(s => s.Discovered)));
        }

        [Test]
        public void Apply_WrongSeed_Throws()
        {
            var data = WorldPersistence.Capture(WorldState.CreateNew(1));

            Assert.Throws<SaveFormatException>(() => WorldPersistence.Apply(WorldState.CreateNew(2), data));
        }

        [Test]
        public void SavedAt_IsIsoTimestamp()
        {
            var data = WorldPersistence.Capture(WorldState.CreateNew(1), 0f, new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc));

            Assert.That(data.SavedAtUtc, Does.StartWith("2026-10-31T17:00:00"));
        }
    }
}
