using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using Parley.Models;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Integration
{
    /// <summary>
    /// Integration tests for ScriptPropertiesPopulator.
    /// Tests action/conditional script population, parameter grid behavior,
    /// and callback invocation patterns.
    ///
    /// In headless tests, FindControl throws InvalidOperationException.
    /// Assert.Throws confirms the populator reached the UI update path.
    /// </summary>
    public class ScriptPropertiesTests
    {
        #region Script Population

        [AvaloniaFact]
        public void PopulateScripts_WithActionScript_ReachesUI()
        {
            var populator = CreateScriptPopulator();
            var node = CreateNodeWithScript("nw_d1_action");
            var safeNode = new TreeViewSafeNode(node);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node, safeNode,
                    NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
        }

        [AvaloniaFact]
        public void PopulateScripts_WithConditionalScript_ReachesUI()
        {
            var node = CreateNodeWithScript("");
            var ptr = new DialogPtr
            {
                ScriptAppears = "nw_d1_cond",
                Node = node
            };
            var safeNode = new TreeViewSafeNode(node, sourcePointer: ptr);
            var populator = CreateScriptPopulator();

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node, safeNode,
                    NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
        }

        [AvaloniaFact]
        public void PopulateScripts_WithBothScripts_ReachesUI()
        {
            var node = CreateNodeWithScript("nw_d1_action");
            var ptr = new DialogPtr
            {
                ScriptAppears = "nw_d1_cond",
                Node = node
            };
            var safeNode = new TreeViewSafeNode(node, sourcePointer: ptr);
            var populator = CreateScriptPopulator();

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node, safeNode,
                    NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
        }

        [AvaloniaFact]
        public void PopulateScripts_NoScripts_ReachesUI()
        {
            var populator = CreateScriptPopulator();
            var node = CreateNodeWithScript("");
            var safeNode = new TreeViewSafeNode(node);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node, safeNode,
                    NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
        }

        [AvaloniaFact]
        public void PopulateScripts_NoSourcePointer_ReachesUI()
        {
            var populator = CreateScriptPopulator();
            var node = CreateNodeWithScript("nw_d1_action");
            var safeNode = new TreeViewSafeNode(node);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node, safeNode,
                    NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
        }

        #endregion

        #region Parameter Grids

        [AvaloniaFact]
        public void PopulateParameterGrids_WithConditionParams_ReachesUI()
        {
            var populator = CreateScriptPopulator();
            var node = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            var ptr = new DialogPtr
            {
                ConditionParams = new Dictionary<string, string>
                {
                    { "nParam1", "42" },
                    { "sParam1", "hello" }
                }
            };

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateParameterGrids(node, ptr,
                    (panel, name, value, isCond) => { }));
        }

        [AvaloniaFact]
        public void PopulateParameterGrids_WithActionParams_ReachesUI()
        {
            var populator = CreateScriptPopulator();
            var node = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            node.ActionParams["sParam1"] = "value1";
            node.ActionParams["nParam1"] = "99";

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateParameterGrids(node, null,
                    (panel, name, value, isCond) => { }));
        }

        [AvaloniaFact]
        public void PopulateParameterGrids_NoParams_ReachesUI()
        {
            var populator = CreateScriptPopulator();
            var node = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateParameterGrids(node, null,
                    (panel, name, value, isCond) => { }));
        }

        [AvaloniaFact]
        public void PopulateParameterGrids_NullPtr_ReachesUI()
        {
            var populator = CreateScriptPopulator();
            var node = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateParameterGrids(node, null,
                    (panel, name, value, isCond) => { }));
        }

        #endregion

        #region Clear

        [AvaloniaFact]
        public void ClearScriptFields_ReachesUI()
        {
            var populator = CreateScriptPopulator();

            Assert.Throws<InvalidOperationException>(() =>
                populator.ClearScriptFields());
        }

        #endregion

        #region Rapid Node Switching

        [AvaloniaFact]
        public void PopulateScripts_RapidSwitch_EachReachesUI()
        {
            var populator = CreateScriptPopulator();

            var node1 = CreateNodeWithScript("nw_d1_action");
            var node2 = CreateNodeWithScript("");
            var node3 = CreateNodeWithScript("nw_d2_action");
            var safe1 = new TreeViewSafeNode(node1);
            var safe2 = new TreeViewSafeNode(node2);
            var safe3 = new TreeViewSafeNode(node3);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node1, safe1, NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node2, safe2, NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node3, safe3, NoOpLoadParams, NoOpLoadPreview, NoOpClearPreview));
        }

        #endregion

        #region Helper Methods

        private static ScriptPropertiesPopulator CreateScriptPopulator()
        {
            var window = new Avalonia.Controls.Window();
            return new ScriptPropertiesPopulator(window);
        }

        private static DialogNode CreateNodeWithScript(string actionScript)
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = "Guard",
                Text = new LocString(),
                ScriptAction = actionScript
            };
            node.Text.Add(0, "Test text");
            return node;
        }

        private static void NoOpLoadParams(string script, bool isConditional) { }
        private static void NoOpLoadPreview(string script, bool isConditional) { }
        private static void NoOpClearPreview(bool isConditional) { }

        #endregion
    }
}
