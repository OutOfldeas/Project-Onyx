using System;
using System.Linq;
using NUnit.Framework;
using Onyx.Core.Audio;

namespace Onyx.Core.Tests.Audio
{
    public class SoundSynthTests
    {
        private static Sfx[] AllSounds => (Sfx[])Enum.GetValues(typeof(Sfx));

        [TestCaseSource(nameof(AllSounds))]
        public void Sound_IsAudible_ShortAndInRange(Sfx sound)
        {
            var s = SoundSynth.Create(sound);

            float rms = (float)Math.Sqrt(s.Average(v => v * v));
            Assert.That(s.Length, Is.GreaterThan(100));
            Assert.That(s.Length / (float)SoundSynth.SampleRate, Is.LessThan(2f));
            Assert.That(s.All(v => v >= -1f && v <= 1f), Is.True);
            Assert.That(rms, Is.GreaterThan(0.01f));
        }

        [Test]
        public void SameVariant_SameSound_OtherVariant_Differs()
        {
            Assert.That(SoundSynth.Create(Sfx.StepSand, 1), Is.EqualTo(SoundSynth.Create(Sfx.StepSand, 1)));
            Assert.That(SoundSynth.Create(Sfx.StepSand, 1), Is.Not.EqualTo(SoundSynth.Create(Sfx.StepSand, 2)));
        }

        [Test]
        public void WindLoop_RepeatsWithoutClick()
        {
            var loop = SoundSynth.WindLoop(4f);

            Assert.That(loop.Length, Is.EqualTo(4 * SoundSynth.SampleRate));
            Assert.That(Math.Abs(loop[0] - loop[loop.Length - 1]), Is.LessThan(0.05f));
        }
    }
}
