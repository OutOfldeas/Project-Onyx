using System;
using NUnit.Framework;
using Onyx.Core.Interaction;

namespace Onyx.Core.Tests.Interaction
{
    public class RepairJobTests
    {
        [Test]
        public void NoWorkers_NoProgress()
        {
            var job = new RepairJob(10f);

            float work = job.Tick(5f);

            Assert.That(work, Is.EqualTo(0f));
            Assert.That(job.Progress, Is.EqualTo(0f));
        }

        [Test]
        public void SingleWorker_ProgressesAtSpeedOne()
        {
            var job = new RepairJob(10f);
            job.Join(1);

            job.Tick(4f);

            Assert.That(job.Progress, Is.EqualTo(4f).Within(0.001f));
            Assert.That(job.Ratio, Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void TwoWorkers_GetTeamBonus()
        {
            var job = new RepairJob(100f);
            job.Join(1);
            job.Join(2);

            job.Tick(1f);

            // 2 Helfer × Tempo 1 × (1 + 0,25) = 2,5 pro Sekunde
            Assert.That(job.Progress, Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void JoiningTwice_CountsOnce()
        {
            var job = new RepairJob(10f);
            job.Join(1);
            job.Join(1);

            Assert.That(job.WorkerCount, Is.EqualTo(1));
        }

        [Test]
        public void Leave_StopsContribution()
        {
            var job = new RepairJob(10f);
            job.Join(1);
            job.Leave(1);

            job.Tick(5f);

            Assert.That(job.Progress, Is.EqualTo(0f));
            Assert.That(job.IsWorking(1), Is.False);
        }

        [Test]
        public void SkillSpeed_IsUsed()
        {
            var job = new RepairJob(100f);
            job.Join(7);

            job.Tick(2f, id => id == 7 ? 1.5f : 1f);

            Assert.That(job.Progress, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void Completion_FiresOnce_AndCapsProgress()
        {
            var job = new RepairJob(5f);
            int completed = 0;
            job.Completed += () => completed++;
            job.Join(1);

            float work = job.Tick(10f);
            job.Tick(10f);

            Assert.That(job.IsComplete, Is.True);
            Assert.That(job.Progress, Is.EqualTo(5f));
            Assert.That(work, Is.EqualTo(5f));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(job.WorkerCount, Is.EqualTo(0));
        }

        [Test]
        public void InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RepairJob(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RepairJob(10f).Tick(-1f));
        }
    }
}
