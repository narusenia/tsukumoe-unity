// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using NUnit.Framework;
using UnityEditor;

namespace Tsukumoe.Tests
{
    public class ModelTests
    {
        const string Fixture = "Packages/com.tsukumoe.unity/Tests/Fixtures/fixture.tmod";

        TsukumoeModel Load() => new TsukumoeModel(AssetDatabase.LoadAssetAtPath<TsukumoeModelAsset>(Fixture).Bytes);

        [Test]
        public void ImporterProducesTheModelAsset()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<TsukumoeModelAsset>(Fixture).Bytes.Length, Is.GreaterThan(0));
        }

        [Test]
        public void ListsParametersMotionsExpressionsAndTextures()
        {
            using var m = Load();
            Assert.AreEqual(5, m.ParameterCount);
            Assert.AreEqual("P", m.ParameterName(0));
            Assert.AreEqual(1, m.ParameterIndex("Q"));
            Assert.AreEqual(1, m.MotionCount);
            Assert.AreEqual("rise", m.MotionName(0));
            Assert.AreEqual("half_q", m.ExpressionName(0));
            Assert.AreEqual(3, m.TextureCount);
        }

        [Test]
        public void MotionRaisesTheParameterAndHeldValuesWin()
        {
            using var m = Load();
            Assert.IsTrue(m.PlayMotion("rise"));
            m.Update(1f);
            Assert.AreEqual(1f, m.ParameterValue(m.ParameterIndex("P")), 1e-3f);
            m.SetParameter(m.ParameterIndex("P"), 0.25f);
            m.Update(0f);
            Assert.AreEqual(0.25f, m.ParameterValue(m.ParameterIndex("P")), 1e-6f);
            m.ClearParameter(m.ParameterIndex("P"));
            m.StopMotion();
        }

        [Test]
        public void ExpressionOverwritesTheParameter()
        {
            using var m = Load();
            m.PlayExpression("half_q");
            m.Update(0.1f);
            Assert.AreEqual(0.5f, m.ParameterValue(m.ParameterIndex("Q")), 1e-3f);
        }

        [Test]
        public void UnknownNamesAreReported()
        {
            using var m = Load();
            var e = Assert.Throws<TsukumoeException>(() => m.PlayMotion("nope"));
            Assert.AreEqual(4, e.Status); // NOT_FOUND
        }

        [Test]
        public void BrokenDataIsRejected()
        {
            var e = Assert.Throws<TsukumoeException>(() => new TsukumoeModel(new byte[] { 1, 2, 3 }));
            Assert.AreEqual(2, e.Status); // INVALID_DATA
        }

        [Test]
        public void AutoBlinkResolvesParameterNames()
        {
            using var m = Load();
            m.SetAuto(AutoMotion.WithBlink("EyeL", "EyeR"));
            Assert.Throws<TsukumoeException>(() => m.SetAuto(AutoMotion.WithBlink("Nope", "EyeR")));
        }
    }
}
