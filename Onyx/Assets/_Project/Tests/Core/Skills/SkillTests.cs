using System;
using NUnit.Framework;
using Onyx.Core.Skills;

namespace Onyx.Core.Tests.Skills
{
    public class SkillTreeTests
    {
        [Test]
        public void DefaultCatalog_IsValid_AndHasHybrids()
        {
            var tree = SkillCatalog.CreateDefault();

            Assert.That(tree.Get("hyb.weakspot").IsHybrid, Is.True);
            Assert.That(tree.Get("eng.repair1").IsHybrid, Is.False);
        }

        [Test]
        public void UnknownPrerequisite_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SkillTree(new[]
            {
                new SkillNode("a", "A", Discipline.Exploration, 1, 1, "", "missing"),
            }));
        }

        [Test]
        public void Cycle_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SkillTree(new[]
            {
                new SkillNode("a", "A", Discipline.Exploration, 1, 1, "", "b"),
                new SkillNode("b", "B", Discipline.Exploration, 1, 1, "", "a"),
            }));
        }
    }

    public class CharacterSkillsTests
    {
        private static CharacterSkills NewSkills() => new CharacterSkills(SkillCatalog.CreateDefault());

        [Test]
        public void Experience_RaisesLevel()
        {
            var skills = NewSkills();
            int levelChanges = 0;
            skills.LevelChanged += (_, __) => levelChanges++;

            skills.AddExperience(Discipline.Engineering, CharacterSkills.ExperienceToNext(0) + 1f);

            Assert.That(skills.Level(Discipline.Engineering), Is.EqualTo(1));
            Assert.That(levelChanges, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedAction_GivesLessExperience()
        {
            var skills = NewSkills();

            float first = skills.AddExperience(Discipline.Engineering, 10f, "repair:panel");
            float second = skills.AddExperience(Discipline.Engineering, 10f, "repair:panel");
            float other = skills.AddExperience(Discipline.Engineering, 10f, "repair:reactor");

            Assert.That(first, Is.EqualTo(10f));
            Assert.That(second, Is.LessThan(first));
            Assert.That(other, Is.EqualTo(10f));
        }

        [Test]
        public void Mentor_GivesBonus()
        {
            var skills = NewSkills();

            float gained = skills.AddExperience(Discipline.Logistics, 10f, mentorLevel: 10);

            Assert.That(gained, Is.EqualTo(15f));
        }

        [Test]
        public void LockedDiscipline_GainsNothing()
        {
            var skills = NewSkills();
            skills.SetLock(Discipline.Diplomacy, SkillLock.Locked);

            Assert.That(skills.AddExperience(Discipline.Diplomacy, 1000f), Is.EqualTo(0f));
            Assert.That(skills.Level(Discipline.Diplomacy), Is.EqualTo(0));
        }

        [Test]
        public void AtCap_WithoutLowerDiscipline_DoesNotLevel()
        {
            var skills = NewSkills();
            skills.DebugSetLevel(Discipline.Exploration, 20);
            skills.DebugSetLevel(Discipline.Engineering, 20);
            skills.DebugSetLevel(Discipline.Diplomacy, 10);
            Assert.That(skills.TotalLevel, Is.EqualTo(CharacterSkills.TotalLevelCap));

            skills.AddExperience(Discipline.Logistics, 100000f);

            Assert.That(skills.Level(Discipline.Logistics), Is.EqualTo(0));
            Assert.That(skills.TotalLevel, Is.EqualTo(CharacterSkills.TotalLevelCap));
        }

        [Test]
        public void AtCap_LowerDiscipline_TransfersLevels()
        {
            var skills = NewSkills();
            skills.DebugSetLevel(Discipline.Exploration, 20);
            skills.DebugSetLevel(Discipline.Engineering, 20);
            skills.DebugSetLevel(Discipline.Diplomacy, 10);
            skills.SetLock(Discipline.Diplomacy, SkillLock.Lower);

            skills.AddExperience(Discipline.Logistics, CharacterSkills.ExperienceToNext(0) + 1f);

            Assert.That(skills.Level(Discipline.Logistics), Is.EqualTo(1));
            Assert.That(skills.Level(Discipline.Diplomacy), Is.EqualTo(9));
            Assert.That(skills.TotalLevel, Is.EqualTo(CharacterSkills.TotalLevelCap));
        }

        [Test]
        public void Unlock_ChecksLevelPointsAndPrerequisites()
        {
            var skills = NewSkills();

            Assert.That(skills.Unlock("eng.repair1"), Is.EqualTo(UnlockCheck.LevelTooLow));

            skills.DebugSetLevel(Discipline.Engineering, 4);
            Assert.That(skills.Unlock("eng.fieldrepair"), Is.EqualTo(UnlockCheck.MissingPrerequisite));
            Assert.That(skills.Unlock("eng.repair1"), Is.EqualTo(UnlockCheck.Ok));
            Assert.That(skills.Unlock("eng.repair1"), Is.EqualTo(UnlockCheck.AlreadyUnlocked));
            Assert.That(skills.Unlock("eng.fieldrepair"), Is.EqualTo(UnlockCheck.Ok));
            Assert.That(skills.AvailablePoints(Discipline.Engineering), Is.EqualTo(1));
            Assert.That(skills.Unlock("eng.reactor"), Is.EqualTo(UnlockCheck.LevelTooLow));
        }

        [Test]
        public void HybridSkill_NeedsBothTrees()
        {
            var skills = NewSkills();
            skills.DebugSetLevel(Discipline.Exploration, 6);
            skills.Unlock("exp.scanner1");
            skills.Unlock("exp.deepscan");

            Assert.That(skills.Unlock("hyb.weakspot"), Is.EqualTo(UnlockCheck.MissingPrerequisite));

            skills.DebugSetLevel(Discipline.Engineering, 4);
            skills.Unlock("eng.repair1");
            skills.Unlock("eng.fieldrepair");

            Assert.That(skills.Unlock("hyb.weakspot"), Is.EqualTo(UnlockCheck.Ok));
        }

        [Test]
        public void LoweredDiscipline_DeactivatesSkill_ButKeepsIt()
        {
            var skills = NewSkills();
            skills.DebugSetLevel(Discipline.Engineering, 6);
            skills.Unlock("eng.repair1");
            skills.Unlock("eng.reactor");
            Assert.That(skills.IsActive("eng.reactor"), Is.True);

            skills.DebugSetLevel(Discipline.Engineering, 5);

            Assert.That(skills.IsUnlocked("eng.reactor"), Is.True);
            Assert.That(skills.IsActive("eng.reactor"), Is.False);
        }
    }
}
