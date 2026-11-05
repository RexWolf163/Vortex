using System.Collections.Generic;
using NUnit.Framework;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Sources;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Tests
{
    /// <summary>Построение переменных индекса (Инв-1, Инв-2, Инв-6) и виды потерь без исключений.</summary>
    internal sealed class IndexAndLossTests
    {
        private static VariableLink Link(string name, NaniVariableOperation operation) =>
            new(name, LinkSource.Nani, operation, null, null, null, new NaniLocation("Assets/Test.nani", 1));

        [Test]
        public void DeclarationWithoutLinks_GivesEntry()
        {
            var entries = VariableIndexBuilder.BuildEntries(
                new List<VariableDeclaration> { new("Day", "0") }, new List<VariableLink>());
            Assert.IsTrue(entries.ContainsKey("Day"));
            Assert.IsEmpty(entries["Day"].Links);
        }

        [Test]
        public void LinkGoesToEntryWithSameName_CaseSensitive()
        {
            var entries = VariableIndexBuilder.BuildEntries(new List<VariableDeclaration>(), new List<VariableLink>
            {
                Link("Day", NaniVariableOperation.Write), Link("day", NaniVariableOperation.Read)
            });
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(1, entries["Day"].Links.Count);
            Assert.AreEqual(1, entries["day"].Links.Count);
        }

        [Test]
        public void Losses_DeclaredUnused()
        {
            var entry = new VariableEntry("Day") { Declaration = new VariableDeclaration("Day", "0") };
            Assert.AreEqual(LossKinds.DeclaredUnused, LossAnalysis.GetRaw(entry));
        }

        [Test]
        public void Losses_UsedUndeclared_AndNeverRead()
        {
            var entry = new VariableEntry("Day");
            entry.AddLink(Link("Day", NaniVariableOperation.Write));
            Assert.AreEqual(LossKinds.UsedUndeclared | LossKinds.NeverRead, LossAnalysis.GetRaw(entry));
        }

        [TestCase(NaniVariableOperation.Compare)]
        [TestCase(NaniVariableOperation.Wait)]
        [TestCase(NaniVariableOperation.Read)]
        public void Losses_NeverWritten(NaniVariableOperation read)
        {
            var entry = new VariableEntry("Day") { Declaration = new VariableDeclaration("Day", "0") };
            entry.AddLink(Link("Day", read));
            Assert.AreEqual(LossKinds.NeverWritten, LossAnalysis.GetRaw(entry));
        }

        [Test]
        public void Losses_WrittenAndRead_None()
        {
            var entry = new VariableEntry("Day") { Declaration = new VariableDeclaration("Day", "0") };
            entry.AddLink(Link("Day", NaniVariableOperation.Write));
            entry.AddLink(Link("Day", NaniVariableOperation.Compare));
            Assert.AreEqual(LossKinds.None, LossAnalysis.GetRaw(entry));
        }

        [Test]
        public void Losses_OtherIsNeitherReadNorWrite()
        {
            var entry = new VariableEntry("Day") { Declaration = new VariableDeclaration("Day", "0") };
            entry.AddLink(Link("Day", NaniVariableOperation.Other));
            Assert.AreEqual(LossKinds.None, LossAnalysis.GetRaw(entry));
        }
    }
}
