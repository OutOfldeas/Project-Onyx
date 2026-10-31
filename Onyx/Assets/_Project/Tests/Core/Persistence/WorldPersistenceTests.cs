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

            world.LocalSkills.DebugSetLevel(Discipline.Engineering, 3);
            world.LocalSkills.AddExperience(Discipline.Engineering, 40f);
            world.LocalSkills.Unlock("eng.repair1");
            world.LocalSkills.SetLock(Discipline.Diplomacy, SkillLock.Lower);
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

            Assert.That(restored.LocalSkills.Level(Discipline.Engineering), Is.EqualTo(3));
            Assert.That(restored.LocalSkills.Experience(Discipline.Engineering), Is.EqualTo(original.LocalSkills.Experience(Discipline.Engineering)).Within(1e-4f));
            Assert.That(restored.LocalSkills.IsUnlocked("eng.repair1"), Is.True);
            Assert.That(restored.LocalSkills.GetLock(Discipline.Diplomacy), Is.EqualTo(SkillLock.Lower));
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
        public void UnknownSkillsAndModules_AreSkipped()
        {
            var data = WorldPersistence.Capture(WorldState.CreateNew(2));
            data.LocalCharacter.Unlocked = new[] { "gibt.es.nicht" };
            data.Ship.Modules = data.Ship.Modules.Concat(new[] { new ModuleSaveData { Id = "warpkern9000", X = 1, Y = 1 } }).ToArray();

            var world = WorldPersistence.Restore(data);

            Assert.That(world.LocalSkills.Unlocked, Is.Empty);
            Assert.That(world.Ship.Modules.Count, Is.EqualTo(data.Ship.Modules.Length - 1));
        }

        [Test]
        public void SavedAt_IsIsoTimestamp()
        {
            var data = WorldPersistence.Capture(WorldState.CreateNew(1), 0f, new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc));

            Assert.That(data.SavedAtUtc, Does.StartWith("2026-10-31T17:00:00"));
        }
    }
}
