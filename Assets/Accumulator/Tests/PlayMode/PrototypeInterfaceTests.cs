using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Accumulator.Core;
using Accumulator.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accumulator.Tests.PlayMode
{
    public sealed class PrototypeInterfaceTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ButtonsTransferCapacityAndPurchaseOnlyOnPlayerAction()
        {
            root = new GameObject("TestPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            Assert.That(controller.Simulation.Ticks, Is.Zero);
            Assert.That(FindButton("Increase2").interactable, Is.False);
            Assert.That(FindButton("Purchase0").interactable, Is.False);
            ClickButton("Decrease0");
            Assert.That(FindButton("Increase2").interactable, Is.True);
            ClickButton("Increase2");
            Assert.That(controller.Simulation.Allocation(0), Is.EqualTo(2));
            Assert.That(controller.Simulation.Allocation(2), Is.EqualTo(2));
            // Restore starting allocation and fund a purchase without any automatic buying.
            ClickButton("Decrease2");
            ClickButton("Increase0");
            controller.Simulation.AdvanceTicks(300);
            controller.Refresh();
            Assert.That(controller.Simulation.Level(0), Is.Zero);
            Assert.That(FindButton("Purchase0").interactable, Is.True);
            ClickButton("Purchase0");
            Assert.That(controller.Simulation.Level(0), Is.EqualTo(1));
            Assert.That(controller.Simulation.A, Is.EqualTo(60 * Economy.Scale));
            Assert.That(controller.Simulation.B, Is.EqualTo(30 * Economy.Scale));
            Assert.That(controller.Simulation.Credits, Is.EqualTo(10 * Economy.Scale));
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(30 * Economy.Scale));
            Assert.That(FindButton("Purchase0").GetComponentInChildren<Text>().text, Does.Contain("Credits"));
        }

        [UnityTest]
        public IEnumerator MilestonesAndStarvationAreVisibleWithoutAddingMechanics()
        {
            root = new GameObject("TestPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            controller.Simulation.AdvanceTicks(20000);
            controller.Refresh();
            var goals = FindText("Milestones").text;
            Assert.That(goals, Does.Contain("[REACHED] 100"));
            Assert.That(goals, Does.Contain("[REACHED] 500"));
            Assert.That(goals, Does.Contain("[REACHED] 2,000"));
            Assert.That(controller.Simulation.Level(0), Is.Zero);
            Assert.That(controller.Simulation.Level(1), Is.Zero);
            Assert.That(controller.Simulation.Level(2), Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(2000 * Economy.Scale));
            ClickButton("ExpandCapacity");
            Assert.That(controller.Simulation.Credits, Is.EqualTo(1880 * Economy.Scale));
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(2000 * Economy.Scale));
            Assert.That(FindText("Milestones").text, Is.EqualTo(goals));
            // Drain all B with no supply, then verify the actual/potential UI describes starvation.
            controller.Simulation.TrySetAllocation(0, 0, out _);
            controller.Simulation.TrySetAllocation(1, 0, out _);
            controller.Simulation.TrySetAllocation(2, 6, out _);
            controller.Simulation.AdvanceTicks(4000);
            controller.Refresh();
            var rates = root.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
            Assert.That(rates.Any(t => t.Contains("INPUT STARVED:") && t.Contains(">B</color>")), Is.True);
            Assert.That(rates.Any(t => t.Contains("0.00 / 6.00")), Is.True);
        }

        [UnityTest]
        public IEnumerator CapacityPurchaseSpendsOnlyCreditsAndUnlocksManualAllocation()
        {
            root = new GameObject("TestPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            Assert.That(FindButton("ExpandCapacity").interactable, Is.False);
            controller.Simulation.AdvanceTicks(1200);
            controller.Refresh();
            Assert.That(controller.Simulation.Capacity, Is.EqualTo(6));
            Assert.That(FindButton("ExpandCapacity").interactable, Is.True);
            Assert.That(FindButton("ExpandCapacity").GetComponentInChildren<Text>().text, Does.Contain("120 Credits"));
            long a = controller.Simulation.A, b = controller.Simulation.B;
            ClickButton("ExpandCapacity");
            Assert.That(controller.Simulation.Capacity, Is.EqualTo(7));
            Assert.That(controller.Simulation.Credits, Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(120 * Economy.Scale));
            Assert.That(controller.Simulation.A, Is.EqualTo(a));
            Assert.That(controller.Simulation.B, Is.EqualTo(b));
            Assert.That(FindButton("ExpandCapacity").interactable, Is.False);
            Assert.That(FindButton("Increase2").interactable, Is.True);
            ClickButton("Increase2");
            Assert.That(controller.Simulation.Allocation(2), Is.EqualTo(2));
            Assert.That(FindButton("Increase2").interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator RunningPresentationActuallyAdvancesExplicitTicks()
        {
            root = new GameObject("TestPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.SetSpeed(20);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(controller.Simulation.Ticks, Is.GreaterThan(0));
            Assert.That(controller.Simulation.LifetimeXp, Is.GreaterThan(0));
            controller.TogglePause();
            long ticks = controller.Simulation.Ticks;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(controller.Simulation.Ticks, Is.EqualTo(ticks));
        }

        private Button FindButton(string name) => root.GetComponentsInChildren<Button>().Single(b => b.name == name);

        [UnityTest]
        public IEnumerator DisablingExperimentBeforeAwakePreservesBaselineInterface()
        {
            root = new GameObject("TestBaseline");
            root.SetActive(false);
            var controller = root.AddComponent<PrototypeController>();
            JsonUtility.FromJsonOverwrite("{\"enableAlternateConversion\":false}", controller);
            root.SetActive(true);
            controller.TogglePause();
            yield return null;
            Assert.That(controller.Simulation.AlternateConversionEnabled, Is.False);
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "ConversionMode"), Is.False);
            controller.ToggleConversionMode();
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            controller.Simulation.AdvanceTicks(10);
            Assert.That(controller.Simulation.Credits, Is.EqualTo(Economy.Scale));
        }

        [UnityTest]
        public IEnumerator ConversionModeButtonSwitchesFreelyAndUpdatesRatesAndUpgradeEffect()
        {
            root = new GameObject("TestPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("2 A"));
            ClickButton("ConversionMode");
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Fast));
            Assert.That(controller.Simulation.Ticks, Is.Zero);
            Assert.That(controller.Simulation.Credits, Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.Zero);
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("4 A"));
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("1.5 B"));
            var effects = root.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
            Assert.That(effects.Any(t => t.Contains("+0.75 B")), Is.True);
            Assert.That(effects.Any(t => t.Contains("2.25 / 3.00")), Is.True, "Fast Conversion must show input-limited actual/max B output.");
            ClickButton("ConversionMode");
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("2 A"));
        }
        private Text FindText(string name) => root.GetComponentsInChildren<Text>().Single(t => t.name == name);

        private void ClickButton(string name)
        {
            Canvas.ForceUpdateCanvases();
            var button = FindButton(name);
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), name + " must receive pointer hits");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
