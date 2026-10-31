using System.Linq;
using NUnit.Framework;
using Onyx.Core.DevTools;
using Onyx.Core.Session;
using Onyx.Core.Skills;

namespace Onyx.Core.Tests.DevTools
{
    public class DevCommandTests
    {
        private WorldState world;
        private DevCommandRegistry registry;

        [SetUp]
        public void SetUp()
        {
            world = WorldState.CreateNew(1234);
            registry = new DevCommandRegistry();
            CoreDevCommands.Register(registry, () => world, w => world = w);
        }

        [Test]
        public void Tokenize_KeepsQuotedText()
        {
            var parts = DevCommandRegistry.Tokenize("say \"hallo welt\"  42");

            Assert.That(parts, Is.EqualTo(new[] { "say", "hallo welt", "42" }));
        }

        [Test]
        public void UnknownCommand_ExplainsHelp()
        {
            Assert.That(registry.Execute("fliegen"), Does.Contain("help"));
        }

        [Test]
        public void Help_ListsCommands()
        {
            string help = registry.Execute("help");

            Assert.That(help, Does.Contain("jump"));
            Assert.That(help, Does.Contain("xp"));
        }

        [Test]
        public void MissingArgument_ShowsUsage()
        {
            Assert.That(registry.Execute("jump"), Does.Contain("Aufruf: jump <systemId>"));
        }

        [Test]
        public void Xp_RaisesLevel()
        {
            registry.Execute("xp technik 500");

            Assert.That(world.LocalSkills.Level(Discipline.Engineering), Is.GreaterThan(0));
        }

        [Test]
        public void Jump_MovesShip()
        {
            var lane = world.Galaxy.LanesOf(world.Navigation.CurrentSystemId).OrderBy(l => l.Distance).First();
            int target = lane.Other(world.Navigation.CurrentSystemId);

            registry.Execute("repair all");
            registry.Execute("fuel");
            registry.Execute($"jump {target}");

            Assert.That(world.Navigation.CurrentSystemId, Is.EqualTo(target));
        }

        [Test]
        public void Range_SetsJumpRange()
        {
            registry.Execute("range 33,5");

            Assert.That(world.Navigation.JumpRange, Is.EqualTo(33.5f).Within(0.001f));
        }

        [Test]
        public void Repair_AllStations_MakesFrigateFlyable()
        {
            Assert.That(world.Navigation.JumpRange, Is.EqualTo(0f));

            string answer = registry.Execute("repair all");

            Assert.That(world.CrashSite.AllRepaired, Is.True);
            Assert.That(world.Navigation.JumpRange, Is.GreaterThan(0f));
            Assert.That(answer, Does.Contain("startklar"));
        }

        [Test]
        public void NewWorld_ReplacesWorld()
        {
            registry.Execute("newworld 777");

            Assert.That(world.Seed, Is.EqualTo(777));
        }

        [Test]
        public void Place_ChangesShip_AndUpdatesNavigation()
        {
            registry.Execute("repair all");
            float rangeBefore = world.Navigation.JumpRange;

            string answer = registry.Execute("place engine 6 11 r");
            registry.Execute("place reactor 20 11");

            Assert.That(answer, Does.Contain("platziert"));
            Assert.That(world.Navigation.JumpRange, Is.GreaterThan(rangeBefore));
        }

        [Test]
        public void Time_AcceptsCommaDecimal()
        {
            registry.Execute("time 0,5");

            Assert.That(world.Clock.TimeOfDay, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
