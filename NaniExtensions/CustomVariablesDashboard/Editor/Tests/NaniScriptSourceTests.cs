using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Sources;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Tests
{
    /// <summary>Разбор сценариев .nani: виды операций, операторы и операнды, что переменной не считается.</summary>
    internal sealed class NaniScriptSourceTests
    {
        private static List<VariableLink> Parse(string text)
        {
            var links = new List<VariableLink>();
            NaniScriptSource.Collect("Assets/Test.nani", text, links);
            return links;
        }

        private static VariableLink Single(List<VariableLink> links, string name, NaniVariableOperation operation) =>
            links.Single(l => l.VariableName == name && l.Operation == operation);

        [TestCase("@set Day=1", "=", "1")]
        [TestCase("@set Day++", "++", "")]
        [TestCase("@set Day--", "--", "")]
        [TestCase("@set Day+=2", "+=", "2")]
        public void Set_IsWriteWithOperator(string line, string op, string operand)
        {
            var link = Single(Parse(line), "Day", NaniVariableOperation.Write);
            Assert.AreEqual(op, link.OperatorText);
            Assert.AreEqual(operand, link.Operand);
        }

        [Test]
        public void Set_RightSideIsRead()
        {
            var links = Parse("@set a=b+1");
            Single(links, "a", NaniVariableOperation.Write);
            Single(links, "b", NaniVariableOperation.Read);
        }

        [Test]
        public void IfParameter_OnAnyCommand_FunctionAndStringAreNotVariables()
        {
            var links = Parse("@set CardsUnlocked++ if:isUnlocked(\"VArtCop00\")");
            Assert.AreEqual(1, links.Count);
            Single(links, "CardsUnlocked", NaniVariableOperation.Write);
        }

        [TestCase("@if Day>=3")]
        [TestCase("@elseif Day>=3")]
        [TestCase("@goto Label if:Day>=3")]
        public void Condition_IsCompareWithOperatorAndOperand(string line)
        {
            var link = Single(Parse(line), "Day", NaniVariableOperation.Compare);
            Assert.AreEqual(">=", link.OperatorText);
            Assert.AreEqual("3", link.Operand);
        }

        [Test]
        public void Condition_SingleEqualsIsComparison()
        {
            var link = Single(Parse("@goto .play if:MeanStreetsFirst=1"), "MeanStreetsFirst", NaniVariableOperation.Compare);
            Assert.AreEqual("=", link.OperatorText);
            Assert.AreEqual("1", link.Operand);
        }

        [Test]
        public void Condition_InBracesWithSpaces()
        {
            var links = Parse("@goto Quest release! if:{MeanStreetsFirst=5 && Dexterity>=70}");
            var first = Single(links, "MeanStreetsFirst", NaniVariableOperation.Compare);
            Assert.AreEqual("=", first.OperatorText);
            Assert.AreEqual("5", first.Operand);
            var dexterity = Single(links, "Dexterity", NaniVariableOperation.Compare);
            Assert.AreEqual(">=", dexterity.OperatorText);
            Assert.AreEqual("70", dexterity.Operand);
        }

        [Test]
        public void Condition_LeftOperandIsFlipped()
        {
            var link = Single(Parse("@if 3<Day"), "Day", NaniVariableOperation.Compare);
            Assert.AreEqual(">", link.OperatorText);
            Assert.AreEqual("3", link.Operand);
        }

        [TestCase("@if Flag", "true")]
        [TestCase("@if !Flag", "false")]
        public void Condition_StandaloneFlag(string line, string operand)
        {
            var link = Single(Parse(line), "Flag", NaniVariableOperation.Compare);
            Assert.AreEqual("==", link.OperatorText);
            Assert.AreEqual(operand, link.Operand);
        }

        [Test]
        public void Condition_ArithmeticAndFunctionArgumentAreRead()
        {
            var links = Parse("@if a+1>=3&&check(b)");
            Single(links, "a", NaniVariableOperation.Read);
            Single(links, "b", NaniVariableOperation.Read);
        }

        [Test]
        public void Condition_BothSidesVariables()
        {
            var links = Parse("@if a>b");
            Assert.AreEqual("b", Single(links, "a", NaniVariableOperation.Compare).Operand);
            var right = Single(links, "b", NaniVariableOperation.Compare);
            Assert.AreEqual("<", right.OperatorText);
            Assert.AreEqual("a", right.Operand);
        }

        [Test]
        public void TrueFalseLiteralsAreNotVariables()
        {
            var links = Parse("@set Flag=true");
            Assert.AreEqual(1, links.Count);
            Single(links, "Flag", NaniVariableOperation.Write);
        }

        [Test]
        public void InlineCommandInTextLine()
        {
            var links = Parse("Hero: Hello [set Met=true] there");
            Single(links, "Met", NaniVariableOperation.Write);
        }

        [Test]
        public void CommentsAndLabelsAreSkipped()
        {
            Assert.IsEmpty(Parse("; @set Day=1\n# Day"));
        }

        [Test]
        public void EqualityIsNotAssignment()
        {
            var links = Parse("@set a==b");
            Assert.IsFalse(links.Any(l => l.Operation == NaniVariableOperation.Write));
            Assert.IsTrue(links.All(l => l.Operation == NaniVariableOperation.Other));
        }

        [Test]
        public void LocationIsPathAndLine()
        {
            var link = Parse("\n\n@set Day=1").Single();
            var location = (NaniLocation)link.Location;
            Assert.AreEqual("Assets/Test.nani", location.ScriptPath);
            Assert.AreEqual(3, location.Line);
        }
    }
}
