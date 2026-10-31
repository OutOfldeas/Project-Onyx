using NUnit.Framework;
using Onyx.Core.Interaction;
using Onyx.Core.Skills;

namespace Onyx.Core.Tests.Skills
{
    public class SkillEffectsTests
    {
        [Test]
        public void RepairSpeed_GrowsWithLevelAndSkill()
        {
            var skills = new CharacterSkills(SkillCatalog.CreateDefault());
            Assert.That(SkillEffects.RepairSpeed(skills), Is.EqualTo(1f));

            skills.DebugSetLevel(Discipline.Engineering, 2);
            float withLevels = SkillEffects.RepairSpeed(skills);
            skills.Unlock("eng.repair1");

            Assert.That(withLevels, Is.EqualTo(1.06f).Within(1e-4f));
            Assert.That(SkillEffects.RepairSpeed(skills), Is.EqualTo(1.26f).Within(1e-4f));
        }

        [Test]
        public void Lightfoot_ReducesOxygenUse()
        {
            var skills = new CharacterSkills(SkillCatalog.CreateDefault());
            Assert.That(SkillEffects.OxygenUseFactor(skills), Is.EqualTo(1f));

            skills.DebugSetLevel(Discipline.Exploration, 2);
            skills.Unlock("exp.lightfoot");

            Assert.That(SkillEffects.OxygenUseFactor(skills), Is.EqualTo(0.8f));
        }

        [Test]
        public void RepairJob_ReportsWorkPerWorker_BySpeed()
        {
            var job = new RepairJob(100f);
            job.Join(1);
            job.Join(2);
            float one = 0f, two = 0f;

            job.Tick(1f, id => id == 1 ? 1f : 3f, (id, work) =>
            {
                if (id == 1) one += work; else two += work;
            });

            Assert.That(one + two, Is.EqualTo(job.Progress).Within(1e-4f));
            Assert.That(two, Is.EqualTo(one * 3f).Within(1e-4f));
        }
    }
}
