using System;
using NUnit.Framework;
using Onyx.Core.Survival;

namespace Onyx.Core.Tests.Survival
{
    public class ResourcePoolTests
    {
        [Test]
        public void NewPool_IsFull()
        {
            var pool = new ResourcePool(100f);

            Assert.That(pool.Current, Is.EqualTo(100f));
            Assert.That(pool.IsFull, Is.True);
            Assert.That(pool.Ratio, Is.EqualTo(1f));
        }

        [Test]
        public void StartValue_IsClampedToRange()
        {
            Assert.That(new ResourcePool(100f, 250f).Current, Is.EqualTo(100f));
            Assert.That(new ResourcePool(100f, -5f).Current, Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Max_MustBePositive(float max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourcePool(max));
        }

        [Test]
        public void Drain_ReducesAndReturnsAmount()
        {
            var pool = new ResourcePool(100f);

            float taken = pool.Drain(30f);

            Assert.That(taken, Is.EqualTo(30f));
            Assert.That(pool.Current, Is.EqualTo(70f));
        }

        [Test]
        public void Drain_MoreThanAvailable_EmptiesAndReturnsRest()
        {
            var pool = new ResourcePool(100f, 20f);

            float taken = pool.Drain(50f);

            Assert.That(taken, Is.EqualTo(20f));
            Assert.That(pool.IsEmpty, Is.True);
        }

        [Test]
        public void Refill_StopsAtMax()
        {
            var pool = new ResourcePool(100f, 90f);

            float added = pool.Refill(25f);

            Assert.That(added, Is.EqualTo(10f));
            Assert.That(pool.IsFull, Is.True);
        }

        [Test]
        public void NegativeOrNaNAmounts_Throw()
        {
            var pool = new ResourcePool(100f);

            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Drain(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Refill(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Drain(float.NaN));
        }

        [Test]
        public void SetMax_KeepRatio_ScalesCurrent()
        {
            var pool = new ResourcePool(100f, 50f);

            pool.SetMax(200f, keepRatio: true);

            Assert.That(pool.Max, Is.EqualTo(200f));
            Assert.That(pool.Current, Is.EqualTo(100f));
        }

        [Test]
        public void SetMax_WithoutKeepRatio_KeepsAmountButClamps()
        {
            var bigger = new ResourcePool(100f, 50f);
            bigger.SetMax(200f);
            Assert.That(bigger.Current, Is.EqualTo(50f));

            var smaller = new ResourcePool(100f, 80f);
            smaller.SetMax(60f);
            Assert.That(smaller.Current, Is.EqualTo(60f));
        }
    }
}
