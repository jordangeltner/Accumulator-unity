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
        public IEnumerator LifetimeXpAndStarvationStayVisibleWithOptionalRateDetails()
        {
            root = new GameObject("TestPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            Assert.That(controller.ShowDetails, Is.False);
            Assert.That(root.GetComponentsInChildren<Text>(true).Any(t => t.name == "Milestones"), Is.False);
            Assert.That(root.GetComponentsInChildren<Text>(true).Any(t => t.name == "ThroughputLegend" || t.name == "CostLegend"), Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("6.00/s")), Is.True);
            controller.Simulation.AdvanceTicks(20000);
            controller.Refresh();
            Assert.That(FindText("Balances").text, Does.Contain("Lifetime XP"));
            Assert.That(FindText("Balances").text, Does.Contain("2,000.00"));
            Assert.That(controller.Simulation.Level(0), Is.Zero);
            Assert.That(controller.Simulation.Level(1), Is.Zero);
            Assert.That(controller.Simulation.Level(2), Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(2000 * Economy.Scale));
            yield return null;
            ClickButton("ExpandCapacity");
            Assert.That(controller.Simulation.Credits, Is.EqualTo(1880 * Economy.Scale));
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(2000 * Economy.Scale));
            // Drain B with no supply. The short warning stays visible without details.
            controller.Simulation.TrySetAllocation(0, 0, out _);
            controller.Simulation.TrySetAllocation(1, 0, out _);
            controller.Simulation.TrySetAllocation(2, 6, out _);
            controller.Simulation.AdvanceTicks(4000);
            controller.Refresh();
            var rates = root.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
            Assert.That(rates.Any(t => t.Contains("Need ") && t.Contains(">B</color>")), Is.True);
            Assert.That(rates.Any(t => t.Contains("0.00 / 6.00")), Is.False);
            ClickButton("Details");
            Assert.That(controller.ShowDetails, Is.True);
            rates = root.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
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
            JsonUtility.FromJsonOverwrite("{\"enableAlternateConversion\":false,\"enableTimingProduction\":false}", controller);
            root.SetActive(true);
            controller.TogglePause();
            yield return null;
            Assert.That(controller.Simulation.AlternateConversionEnabled, Is.False);
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "ConversionMode"), Is.False);
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "ProductionMode"), Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.name == "NextUnlock"), Is.False);
            Assert.That(controller.Simulation.TimingProductionEnabled, Is.False);
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
            SeedUnlockedModes(controller);
            yield return null;
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(controller.ShowDetails, Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.name == "ConversionModeDetails" || t.name == "ProductionModeDetails"), Is.False);
            ClickButton("Details");
            Assert.That(controller.ShowDetails, Is.True);
            yield return null;
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("2 A"));
            ClickButton("ConversionMode");
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Fast));
            Assert.That(controller.Simulation.Ticks, Is.Zero);
            Assert.That(controller.Simulation.Credits, Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(Economy.TimingProductionUnlockXp * Economy.Scale));
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("4 A"));
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("1.5 B"));
            var effects = root.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
            Assert.That(effects.Any(t => t.Contains("+0.75 B")), Is.True);
            Assert.That(effects.Any(t => t.Contains("2.25 / 3.00")), Is.True, "Fast Conversion must show input-limited actual/max B output.");
            ClickButton("ConversionMode");
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("2 A"));
            ClickButton("Details");
            Assert.That(controller.ShowDetails, Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.name == "ConversionModeDetails" || t.name == "ProductionModeDetails"), Is.False);
        }
        private Text FindText(string name) => root.GetComponentsInChildren<Text>().Single(t => t.name == name);

        [UnityTest]
        public IEnumerator TimingBarClickAwardsAAndCannotRepeatOrBypassPause()
        {
            root = new GameObject("TestTimingPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            SeedUnlockedModes(controller);
            yield return null;
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "TimingBar"), Is.False);
            ClickButton("ProductionMode");
            Assert.That(controller.Simulation.SelectedProductionMode, Is.EqualTo(ProductionMode.Timing));
            yield return null; // Allow the newly expanded layout to complete while paused.
            controller.Simulation.AdvanceTimingMilliseconds(750);
            controller.Refresh();
            Assert.That(FindButton("TimingBar").interactable, Is.False);
            controller.StrikeTiming();
            Assert.That(controller.Simulation.TimingAttempts, Is.Zero);
            var marker = root.GetComponentsInChildren<RectTransform>().Single(t => t.name == "TimingMarker");
            Assert.That(marker.anchorMin.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(marker.rect.width, Is.GreaterThan(0));
            controller.TogglePause();
            Assert.That(FindButton("TimingBar").interactable, Is.True);
            ClickButton("TimingBar");
            Assert.That(controller.Simulation.A, Is.EqualTo(18 * Economy.Scale));
            Assert.That(controller.Simulation.Credits, Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(Economy.TimingProductionUnlockXp * Economy.Scale));
            Assert.That(controller.Simulation.TimingHits, Is.EqualTo(1));
            Assert.That(controller.Simulation.TimingAttempts, Is.EqualTo(1));
            Assert.That(controller.Simulation.LastTimingResult, Is.EqualTo(TimingResult.Critical));
            Assert.That(FindText("TimingStatus").text, Does.Contain("CRITICAL!"));
            Assert.That(FindText("TimingSuccessLeft").text, Is.EqualTo("SUCCESS"));
            Assert.That(FindText("TimingCriticalLabel").text, Is.EqualTo("CRITICAL"));
            Assert.That(FindButton("TimingBar").interactable, Is.False);
            controller.StrikeTiming();
            Assert.That(controller.Simulation.A, Is.EqualTo(18 * Economy.Scale));
            Assert.That(controller.Simulation.TimingAttempts, Is.EqualTo(1));
            controller.Simulation.AdvanceTimingMilliseconds(1575); // Reverse sweep, position 4500: success outside critical.
            controller.Refresh();
            ClickButton("TimingBar");
            Assert.That(controller.Simulation.LastTimingResult, Is.EqualTo(TimingResult.Success));
            Assert.That(controller.Simulation.A, Is.EqualTo(30 * Economy.Scale));
            Assert.That(controller.Simulation.TimingHits, Is.EqualTo(2));
            Assert.That(FindText("TimingStatus").text, Does.Contain("SUCCESS!"));
            ClickButton("ProductionMode");
            Assert.That(controller.Simulation.SelectedProductionMode, Is.EqualTo(ProductionMode.Passive));
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "TimingBar"), Is.False);
        }

        [UnityTest]
        public IEnumerator TimingModeRestrictsSpeedAndMissesGiveNoReward()
        {
            root = new GameObject("TestTimingPrototype");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            SeedUnlockedModes(controller);
            yield return null;
            controller.SetSpeed(20);
            ClickButton("ProductionMode");
            yield return null;
            Assert.That(controller.Speed, Is.EqualTo(1));
            Assert.That(FindButton("Speed5").interactable, Is.False);
            Assert.That(FindButton("Speed20").interactable, Is.False);
            controller.SetSpeed(20);
            Assert.That(controller.Speed, Is.EqualTo(1));
            controller.TogglePause();
            ClickButton("TimingBar");
            Assert.That(controller.Simulation.TimingHits, Is.Zero);
            Assert.That(controller.Simulation.TimingAttempts, Is.EqualTo(1));
            Assert.That(controller.Simulation.A, Is.Zero);
            Assert.That(FindText("TimingStatus").text, Does.Contain("MISS"));
            ClickButton("ProductionMode");
            Assert.That(FindButton("Speed20").interactable, Is.True);
            controller.SetSpeed(20);
            Assert.That(controller.Speed, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator TimingClockAdvancesAndFreezesWithPause()
        {
            root = new GameObject("TestTimingPrototype");
            var controller = root.AddComponent<PrototypeController>();
            SeedUnlockedModes(controller);
            controller.ToggleProductionMode();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(controller.Simulation.TimingElapsedMilliseconds, Is.GreaterThan(0));
            Assert.That(controller.Simulation.A, Is.Zero, "Timing mode must not produce passive A.");
            controller.TogglePause();
            long elapsed = controller.Simulation.TimingElapsedMilliseconds;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(controller.Simulation.TimingElapsedMilliseconds, Is.EqualTo(elapsed));
        }

        [UnityTest]
        public IEnumerator EarnedXpUnlocksVisibleModesWithoutSpendingOrAutoSwitching()
        {
            root = new GameObject("TestProgression");
            var controller = root.AddComponent<PrototypeController>();
            controller.TogglePause();
            yield return null;
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "ConversionMode" || b.name == "ProductionMode"), Is.False);
            Assert.That(FindText("NextUnlock").text, Does.Contain("Fast Conversion"));
            Assert.That(FindText("NextUnlock").text, Does.Contain("100 XP"));
            controller.ToggleConversionMode();
            controller.ToggleProductionMode();
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(controller.Simulation.SelectedProductionMode, Is.EqualTo(ProductionMode.Passive));
            controller.Simulation.AdvanceTicks(999);
            controller.Refresh();
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "ConversionMode"), Is.False);
            controller.Simulation.AdvanceTicks(1);
            controller.Refresh();
            yield return null;
            Assert.That(FindButton("ConversionMode").interactable, Is.True);
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "ProductionMode"), Is.False);
            Assert.That(FindText("NextUnlock").text, Does.Contain("Timing Production"));
            Assert.That(FindText("NextUnlock").text, Does.Contain("500 XP"));
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            ClickButton("ConversionMode");
            ClickButton("Purchase0");
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(100 * Economy.Scale));
            Assert.That(FindButton("ConversionMode").interactable, Is.True);
            controller.Simulation.AdvanceTicks(4000);
            controller.Refresh();
            yield return null;
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(500 * Economy.Scale));
            Assert.That(FindButton("ProductionMode").interactable, Is.True);
            Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.name == "NextUnlock"), Is.False);
            Assert.That(controller.Simulation.SelectedProductionMode, Is.EqualTo(ProductionMode.Passive));
            long credits = controller.Simulation.Credits;
            ClickButton("ProductionMode");
            Assert.That(controller.Simulation.SelectedProductionMode, Is.EqualTo(ProductionMode.Timing));
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(500 * Economy.Scale));
            Assert.That(controller.Simulation.Credits, Is.EqualTo(credits));
        }

        // Mechanics tests need empty inventories; progression is earned through real ticks in the test above.
        private static void SeedUnlockedModes(PrototypeController controller)
        {
            typeof(Economy).GetProperty(nameof(Economy.LifetimeXp)).GetSetMethod(true)
                .Invoke(controller.Simulation, new object[] { Economy.TimingProductionUnlockXp * Economy.Scale });
            controller.Refresh();
        }

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
