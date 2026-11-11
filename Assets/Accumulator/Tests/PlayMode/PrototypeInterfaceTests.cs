using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Accumulator.Core;
using Accumulator.Presentation;
using Accumulator.Presentation.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Accumulator.Tests.PlayMode
{
    public sealed class PrototypeInterfaceTests
    {
        private GameObject root;
        private PrototypeController controller;
        private string directory, profilePath;
        private float originalVolume;

        [SetUp]
        public void Setup()
        {
            originalVolume = AudioListener.volume;
            directory = Path.Combine(Path.GetTempPath(), "Accumulator06UI-" + Guid.NewGuid().ToString("N"));
            profilePath = Path.Combine(directory, "profile.json");
            CreateController();
        }

        private void CreateController()
        {
            root = new GameObject("TestPrototype06");
            root.SetActive(false);
            controller = root.AddComponent<PrototypeController>();
            controller.enabled = false; // Most checks drive explicit elapsed inputs, independent of test-runner frames.
            controller.UseLocalProfileStore(new LocalProfileStore(profilePath));
            root.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
            AudioListener.volume = originalVolume;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private void Begin(bool practice = false)
        {
            controller.StartRun(practice);
            Assert.That(controller.Session.State, Is.EqualTo(RunState.Running));
        }

        [UnityTest]
        public IEnumerator LandingMenuDoesNotStartTheEconomyAndGuideFollowsNewRun()
        {
            yield return null;
            Assert.That(controller.CurrentScreen, Is.EqualTo("MainMenu"));
            Assert.That(controller.Session, Is.Null);
            controller.AdvanceRunMilliseconds(10000);
            Assert.That(controller.Session, Is.Null);
            var menu = FindButton("NewRun").transform.parent;
            var names = menu.GetComponentsInChildren<Button>().Select(value => value.name).ToArray();
            Assert.That(Array.IndexOf(names, "HowToPlay"), Is.EqualTo(Array.IndexOf(names, "NewRun") + 1));
            Assert.That(names, Does.Contain("Options"));
            Assert.That(names, Does.Contain("Statistics"));
            ClickButton("HowToPlay");
            Assert.That(controller.CurrentScreen, Is.EqualTo("HowToPlay"));
            yield return null; // A newly activated layout is presented on the next frame.
            foreach (var section in new[] { "Goal", "Economy", "Upgrades", "Timing" })
            {
                string text = FindText("HowToPlay" + section).text;
                Assert.That(text, Does.Contain(section).IgnoreCase);
                Assert.That(text, Does.Contain("\n"), section + " must separate its heading from its bullets.");
                Assert.That(text.Contains("\u2022") || text.Contains("\n- "), Is.True, section + " must use bullets.");
            }
            Assert.That(FindText("HowToPlayGoal").text, Does.Contain("2,000"));
            ClickButton("CloseHowToPlay");
            Assert.That(controller.CurrentScreen, Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator NewRunDropdownSupportsPointerKeyboardAndNestedRunOptions()
        {
            yield return null;
            ClickButton("NewRun");
            Assert.That(controller.CurrentScreen, Is.EqualTo("NewRun"));
            Assert.That(FindButton("NewRun").IsInteractable(), Is.False, "The modal must block keyboard navigation into the menu.");
            Assert.That(controller.Session, Is.Null);
            Assert.That(FindText("RunRules").text.Length, Is.LessThan(300), "Keep the run description short.");
            yield return null;
            ClickButton("CancelNewRun");
            Assert.That(controller.CurrentScreen, Is.EqualTo("MainMenu"));
            yield return null;
            ClickButton("NewRun");
            yield return null;
            var dropdown = root.GetComponentsInChildren<Dropdown>().Single(value => value.name == "RunMode");
            Assert.That(dropdown.options.Select(option => option.text), Is.EqualTo(new[] { "Standard", "Practice" }));
            Assert.That(dropdown.value, Is.Zero);
            ClickSelectable(dropdown);
            yield return null;
            ClickSelectable(root.GetComponentsInChildren<Toggle>().Single(value => value.GetComponentInChildren<Text>().text == "Practice"));
            Assert.That(dropdown.value, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.2f); // Wait for the popup blocker to disappear.
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            ExecuteEvents.Execute(dropdown.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected.GetComponent<Toggle>(), Is.Not.Null);
            ExecuteEvents.Execute(selected, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Up, moveVector = Vector2.up }, ExecuteEvents.moveHandler);
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(dropdown.value, Is.Zero, "Keyboard navigation must select Standard.");
            yield return new WaitForSecondsRealtime(0.2f);
            ClickSelectable(dropdown);
            yield return null;
            ClickSelectable(root.GetComponentsInChildren<Toggle>().Single(value => value.GetComponentInChildren<Text>().text == "Practice"));
            yield return new WaitForSecondsRealtime(0.2f);
            ClickButton("RunOptions");
            Assert.That(controller.CurrentScreen, Is.EqualTo("RunOptions"));
            Assert.That(FindButton("BeginRun").IsInteractable(), Is.False);
            Assert.That(dropdown.IsInteractable(), Is.False);
            Assert.That(controller.Session, Is.Null);
            yield return null;
            var settings = FindButton("CloseRunOptions").transform.parent;
            Assert.That(settings.GetComponentsInChildren<Selectable>().Where(value => value.IsInteractable())
                .Select(value => value.name), Is.EqualTo(new[] { "CloseRunOptions" }), "Placeholder run settings must be fixed.");
            ClickButton("CloseRunOptions");
            Assert.That(controller.CurrentScreen, Is.EqualTo("NewRun"));
            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(FindButton("NewRun").IsInteractable(), Is.False);
            yield return null;
            ClickButton("BeginRun");
            Assert.That(controller.CurrentScreen, Is.EqualTo("Gameplay"));
            Assert.That(controller.Session.State, Is.EqualTo(RunState.Running));
            Assert.That(controller.Session.IsPractice, Is.True);
            Assert.That(controller.Session.IsPersonalBestEligible, Is.False);
            Assert.That(controller.Simulation.Ticks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BeginRunStartsImmediatelyAtZeroAndAcceptsGameplayCommands()
        {
            yield return null;
            ClickButton("NewRun");
            yield return null;
            ClickButton("BeginRun");
            Assert.That(controller.Session.State, Is.EqualTo(RunState.Running));
            Assert.That(controller.Session.IsPersonalBestEligible, Is.True);
            Assert.That(controller.Session.ElapsedMilliseconds, Is.Zero);
            Assert.That(controller.Simulation.Ticks, Is.Zero);
            yield return null;
            Assert.That(FindButton("Decrease0").interactable, Is.True);
            ClickButton("Decrease0");
            Assert.That(controller.Simulation.Allocation(0), Is.EqualTo(2));
            controller.AdvanceRunMilliseconds(99);
            Assert.That(controller.Simulation.Ticks, Is.Zero);
            controller.AdvanceRunMilliseconds(1);
            Assert.That(controller.Simulation.Ticks, Is.EqualTo(1));
            Assert.That(FindText("RunHeading").text, Does.Contain("00:00.1"));
        }

        [UnityTest]
        public IEnumerator ButtonsTransferCapacityAndSpendOnlyCreditsOnPlayerAction()
        {
            Begin();
            yield return null;
            Assert.That(FindButton("Increase2").interactable, Is.False);
            Assert.That(FindButton("Purchase0").interactable, Is.False);
            ClickButton("Decrease0");
            ClickButton("Increase2");
            Assert.That(controller.Simulation.Allocation(0), Is.EqualTo(2));
            Assert.That(controller.Simulation.Allocation(2), Is.EqualTo(2));
            ClickButton("Decrease2");
            ClickButton("Increase0");
            controller.AdvanceRunMilliseconds(30000);
            Assert.That(controller.Simulation.Level(0), Is.Zero);
            Assert.That(FindButton("Purchase0").GetComponentInChildren<Text>().text, Does.Contain("20 Credits"));
            ClickButton("Purchase0");
            Assert.That(controller.Simulation.Level(0), Is.EqualTo(1));
            Assert.That(controller.Simulation.A, Is.EqualTo(60 * Economy.Scale));
            Assert.That(controller.Simulation.B, Is.EqualTo(30 * Economy.Scale));
            Assert.That(controller.Simulation.Credits, Is.EqualTo(10 * Economy.Scale));
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(30 * Economy.Scale));
        }

        [UnityTest]
        public IEnumerator CapacityPurchaseUsesCreditsAndPreservesXp()
        {
            Begin();
            controller.AdvanceRunMilliseconds(120000);
            yield return null;
            Assert.That(FindButton("ExpandCapacity").interactable, Is.True);
            long xp = controller.Simulation.LifetimeXp;
            ClickButton("ExpandCapacity");
            Assert.That(controller.Simulation.Capacity, Is.EqualTo(7));
            Assert.That(controller.Simulation.Credits, Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(xp));
            ClickButton("Increase2");
            Assert.That(controller.Simulation.Allocation(2), Is.EqualTo(2));
            Assert.That(FindButton("Increase2").interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator EarnedXpDisclosesModesAndDetailsWithoutSpendingOrSwitching()
        {
            Begin();
            yield return null;
            Assert.That(HasButton("ConversionMode"), Is.False);
            Assert.That(HasButton("ProductionMode"), Is.False);
            Assert.That(FindText("NextUnlock").text, Does.Contain("100 XP"));
            controller.AdvanceRunMilliseconds(99900);
            Assert.That(HasButton("ConversionMode"), Is.False);
            controller.AdvanceRunMilliseconds(100);
            Assert.That(HasButton("ConversionMode"), Is.True);
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Standard));
            Assert.That(FindText("NextUnlock").text, Does.Contain("500 XP"));
            yield return null;
            ScrollTo("ConversionMode");
            ClickButton("ConversionMode");
            Assert.That(controller.Simulation.SelectedConversionMode, Is.EqualTo(ConversionMode.Fast));
            ClickButton("Details");
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("4 A"));
            Assert.That(FindText("ConversionModeDetails").text, Does.Contain("1.5 B"));
            controller.AdvanceRunMilliseconds(400000);
            Assert.That(HasButton("ProductionMode"), Is.True);
            Assert.That(controller.Simulation.SelectedProductionMode, Is.EqualTo(ProductionMode.Passive));
            Assert.That(controller.Simulation.LifetimeXp, Is.EqualTo(500 * Economy.Scale));
            Assert.That(root.GetComponentsInChildren<Text>().Any(value => value.name == "NextUnlock"), Is.False);
        }

        [UnityTest]
        public IEnumerator TimingBarGradesActualPointerHitsAndRejectsRepeatedAttempts()
        {
            Begin();
            controller.AdvanceRunMilliseconds(500000);
            yield return null;
            ScrollTo("ProductionMode");
            ClickButton("ProductionMode");
            controller.AdvanceRunMilliseconds(750);
            yield return null;
            var marker = root.GetComponentsInChildren<RectTransform>().Single(value => value.name == "TimingMarker");
            Assert.That(marker.anchorMin.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(marker.rect.width, Is.GreaterThan(0));
            long before = controller.Simulation.A;
            ScrollTo("TimingBar");
            ClickButton("TimingBar");
            Assert.That(controller.Simulation.A, Is.EqualTo(before + 18 * Economy.Scale));
            Assert.That(controller.Simulation.LastTimingResult, Is.EqualTo(TimingResult.Critical));
            Assert.That(FindText("TimingStatus").text, Does.Contain("CRITICAL!"));
            controller.StrikeTiming();
            Assert.That(controller.Simulation.TimingAttempts, Is.EqualTo(1));
            controller.AdvanceRunMilliseconds(1575);
            ClickButton("TimingBar");
            Assert.That(controller.Simulation.LastTimingResult, Is.EqualTo(TimingResult.Success));
            Assert.That(controller.Simulation.TimingAttempts, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PracticeAccelerationIsRestrictedWhileTimingAndMissesDoNotReward()
        {
            Begin(true);
            controller.AdvanceRunMilliseconds(500000);
            controller.Session.TrySetSpeed(20, out _);
            controller.Refresh();
            controller.ToggleProductionMode();
            yield return null;
            Assert.That(controller.Speed, Is.EqualTo(1));
            Assert.That(HasButton("Speed20"), Is.False);
            long before = controller.Simulation.A;
            controller.StrikeTiming();
            Assert.That(controller.Simulation.LastTimingResult, Is.EqualTo(TimingResult.Miss));
            Assert.That(controller.Simulation.A, Is.EqualTo(before));
            controller.Session.TrySetSpeed(20, out _);
            controller.Refresh();
            Assert.That(controller.Speed, Is.EqualTo(1));
            controller.ToggleProductionMode();
            controller.Session.TrySetSpeed(20, out _);
            controller.Refresh();
            Assert.That(controller.Speed, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator FocusLossAndSuspendOpenOptionsFreezeCommandsAndRequireExplicitReturn()
        {
            Begin();
            controller.AdvanceRunMilliseconds(500);
            root.SendMessage("OnApplicationFocus", false);
            Assert.That(controller.CurrentScreen, Is.EqualTo("Options"));
            controller.ChangeAllocation(0, -1);
            controller.AdvanceRunMilliseconds(5000);
            yield return null;
            Assert.That(controller.Session.ElapsedMilliseconds, Is.EqualTo(500));
            Assert.That(controller.Simulation.Allocation(0), Is.EqualTo(3));
            Assert.That(controller.Session.IsPersonalBestEligible, Is.False);
            Assert.That(controller.Paused, Is.True);
            root.SendMessage("OnApplicationFocus", true);
            Assert.That(controller.Paused, Is.True, "Focus return must not silently resume.");
            ClickButton("CloseOptions");
            Assert.That(controller.Paused, Is.False);
            controller.AdvanceRunMilliseconds(100);
            root.SendMessage("OnApplicationPause", true);
            Assert.That(controller.CurrentScreen, Is.EqualTo("Options"));
            Assert.That(controller.Paused, Is.True);
            root.SendMessage("OnApplicationPause", false);
            controller.AdvanceRunMilliseconds(5000);
            Assert.That(controller.Session.ElapsedMilliseconds, Is.EqualTo(600));
            yield return null;
            ClickButton("CloseOptions");
            Assert.That(controller.Paused, Is.False);
            Assert.That(controller.Session.IsPersonalBestEligible, Is.False);
        }

        [UnityTest]
        public IEnumerator OptionsPauseRunAndBackToRunResumesWithoutRestoringPbEligibility()
        {
            Begin();
            controller.AdvanceRunMilliseconds(300);
            controller.OpenOptions();
            yield return null;
            Assert.That(controller.CurrentScreen, Is.EqualTo("Options"));
            Assert.That(controller.Paused, Is.True);
            controller.AdvanceRunMilliseconds(5000);
            ClickButton("CloseOptions");
            Assert.That(controller.CurrentScreen, Is.EqualTo("Gameplay"));
            Assert.That(controller.Paused, Is.False);
            Assert.That(controller.Session.ElapsedMilliseconds, Is.EqualTo(300));
            Assert.That(controller.Session.IsPersonalBestEligible, Is.False);
            controller.AdvanceRunMilliseconds(100);
            Assert.That(controller.Session.ElapsedMilliseconds, Is.EqualTo(400));
        }

        [UnityTest]
        public IEnumerator CompletionSavesPbOnceAndRetryResetsAllRunState()
        {
            Begin();
            controller.AdvanceRunMilliseconds(2000000);
            yield return null;
            var result = controller.Session.Result;
            Assert.That(controller.CurrentScreen, Is.EqualTo("Results"));
            Assert.That(FindText("RunResult").text, Does.Contain("NEW PERSONAL BEST"));
            Assert.That(FindText("RunResult").text, Does.Contain("Personal best saved."));
            Assert.That(result.ElapsedMilliseconds, Is.EqualTo(2000000));
            Assert.That(controller.Profile.FindBest(result.CategoryKey).elapsedMilliseconds, Is.EqualTo(result.ElapsedMilliseconds));
            Assert.That(File.Exists(profilePath), Is.True);
            controller.AdvanceRunMilliseconds(10000);
            Assert.That(controller.Session.Result, Is.SameAs(result));
            ClickButton("QuickRetry");
            Assert.That(controller.Session.State, Is.EqualTo(RunState.Running));
            Assert.That(controller.Session.ElapsedMilliseconds, Is.Zero);
            Assert.That(controller.Simulation.LifetimeXp, Is.Zero);
            Assert.That(controller.Simulation.TimingAttempts, Is.Zero);
            Assert.That(controller.Profile.FindBest(result.CategoryKey), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PracticeCompletionDoesNotCreatePb()
        {
            Begin(true);
            controller.Session.TrySetSpeed(20, out _);
            controller.Refresh();
            controller.AdvanceRunMilliseconds(100000);
            yield return null;
            Assert.That(controller.CurrentScreen, Is.EqualTo("Results"));
            Assert.That(controller.Profile.PersonalBests.Count, Is.Zero);
            Assert.That(FindText("RunResult").text, Does.Contain("PB ineligible"));
            Assert.That(FindText("RunResult").text, Does.Contain("PB excluded: Practice run."));
        }

        [UnityTest]
        public IEnumerator ExcludedCompletionExplainsWhyNoPbWasSaved()
        {
            Begin();
            controller.OpenOptions();
            controller.CloseOptions();
            controller.AdvanceRunMilliseconds(2000000);
            yield return null;
            Assert.That(controller.CurrentScreen, Is.EqualTo("Results"));
            Assert.That(FindText("RunResult").text, Does.Contain("PB excluded: Options opened; practice only."));
            Assert.That(FindText("RunResult").text, Does.Not.Contain("Personal best saved."));
            Assert.That(controller.Profile.PersonalBests, Is.Empty);
            Assert.That(File.Exists(profilePath), Is.False);
        }

        [UnityTest]
        public IEnumerator FailedFirstPbSaveIsNotPublishedInResultsMenuOrStatistics()
        {
            Directory.CreateDirectory(profilePath + ".tmp"); // Block the atomic temporary write.
            Begin();
            controller.AdvanceRunMilliseconds(2000000);
            yield return null;
            Assert.That(FindText("RunResult").text, Does.Contain("Personal best not saved."));
            Assert.That(FindText("RunResult").text, Does.Contain("Could not save local profile"));
            Assert.That(FindText("RunResult").text, Does.Not.Contain("NEW PERSONAL BEST"));
            Assert.That(controller.Profile.PersonalBests, Is.Empty);
            Assert.That(File.Exists(profilePath), Is.False);
            controller.ReturnToMainMenu();
            Assert.That(FindText("MenuBest").text, Does.Contain("No saved eligible run yet"));
            yield return null;
            ClickButton("Statistics");
            Assert.That(FindText("PersonalStatistics").text, Does.Not.Contain("33:20.0"));
        }

        [UnityTest]
        public IEnumerator FailedImprovementPreservesSavedPbAndLaterAttemptCanSave()
        {
            Begin();
            Assert.That(controller.Session.TrySetAllocation(Economy.Developer, 0, out _), Is.True);
            controller.AdvanceRunMilliseconds(500);
            Assert.That(controller.Session.TrySetAllocation(Economy.Developer, 1, out _), Is.True);
            controller.AdvanceRunMilliseconds(2000000);
            string category = controller.Session.Result.CategoryKey;
            string savedProfile = File.ReadAllText(profilePath);
            Directory.CreateDirectory(profilePath + ".tmp");
            controller.RestartRun();
            controller.AdvanceRunMilliseconds(2000000);
            yield return null;
            Assert.That(FindText("RunResult").text, Does.Contain("Personal best not saved."));
            Assert.That(FindText("RunResult").text, Does.Contain("Personal best: 33:20.5"));
            Assert.That(controller.Profile.FindBest(category).elapsedMilliseconds, Is.EqualTo(2000500));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(savedProfile));
            Assert.That(new LocalProfileStore(profilePath).Load(out _).FindBest(category).elapsedMilliseconds, Is.EqualTo(2000500));

            Directory.Delete(profilePath + ".tmp");
            controller.OpenOptions();
            yield return null;
            ClickButton("VolumeDown");
            controller.CloseOptions();
            Assert.That(FindText("RunResult").text, Does.Contain("Personal best not saved."), "An option save must not relabel the failed run save.");
            Assert.That(new LocalProfileStore(profilePath).Load(out _).FindBest(category).elapsedMilliseconds, Is.EqualTo(2000500));
            controller.RestartRun();
            controller.AdvanceRunMilliseconds(2000000);
            Assert.That(FindText("RunResult").text, Does.Contain("Personal best saved."));
            Assert.That(controller.Profile.FindBest(category).elapsedMilliseconds, Is.EqualTo(2000000));
            Assert.That(new LocalProfileStore(profilePath).Load(out _).FindBest(category).elapsedMilliseconds, Is.EqualTo(2000000));
        }

        [UnityTest]
        public IEnumerator TiedAndSlowerResultsDoNotClaimAnotherPbSave()
        {
            Begin();
            controller.AdvanceRunMilliseconds(2000000);
            string savedProfile = File.ReadAllText(profilePath);
            Directory.CreateDirectory(profilePath + ".tmp"); // No write should be attempted for these results.
            controller.RestartRun();
            controller.AdvanceRunMilliseconds(2000000);
            yield return null;
            Assert.That(FindText("RunResult").text, Does.Contain("No PB improvement; saved personal best unchanged."));
            Assert.That(FindText("RunResult").text, Does.Not.Contain("NEW PERSONAL BEST"));
            Assert.That(FindText("RunResult").text, Does.Not.Contain("Personal best saved."));
            controller.RestartRun();
            controller.Session.TrySetAllocation(Economy.Developer, 0, out _);
            controller.AdvanceRunMilliseconds(500);
            controller.Session.TrySetAllocation(Economy.Developer, 1, out _);
            controller.AdvanceRunMilliseconds(2000000);
            Assert.That(FindText("RunResult").text, Does.Contain("No PB improvement; saved personal best unchanged."));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(savedProfile));
        }

        [UnityTest]
        public IEnumerator PreferencesAndPbSurviveControllerRecreationAndAppearInStatistics()
        {
            yield return null;
            controller.OpenOptions();
            yield return null;
            ClickButton("VolumeDown");
            ClickButton("DefaultDetails");
            controller.CloseOptions();
            Begin();
            controller.AdvanceRunMilliseconds(2000000);
            Object.Destroy(root);
            yield return null;
            CreateController();
            yield return null;
            Assert.That(controller.ShowDetails, Is.True);
            Assert.That(controller.Profile.Options.masterVolume, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(controller.Session, Is.Null);
            ClickButton("Statistics");
            Assert.That(FindText("PersonalStatistics").text, Does.Contain("33:20.0"));
            Assert.That(FindText("PersonalStatistics").text, Does.Contain("Global leaderboards arrive in 0.10"));
        }

        [UnityTest]
        public IEnumerator FrameUpdatesActuallyAdvanceRunningSession()
        {
            Begin();
            controller.enabled = true;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.Simulation.Ticks, Is.GreaterThan(0));
            controller.OpenOptions();
            long elapsed = controller.Session.ElapsedMilliseconds;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(controller.Session.ElapsedMilliseconds, Is.EqualTo(elapsed));
            controller.enabled = false;
        }

        [UnityTest]
        public IEnumerator GameplayToolbarStaysAtBottomRightOutsideScrollWhenTimingExpands()
        {
            Begin();
            yield return null;
            foreach (var name in new[] { "Pause", "Speed1", "Speed5", "Speed20" })
                Assert.That(HasButton(name), Is.False);
            var toolbar = root.GetComponentsInChildren<RectTransform>().Single(value => value.name == "GameplayToolbar");
            var scroll = root.GetComponentsInChildren<ScrollRect>().Single();
            Assert.That(toolbar.GetComponentInParent<ScrollRect>(), Is.Null);
            Assert.That(toolbar.parent, Is.SameAs(scroll.transform.parent));
            foreach (var name in new[] { "GameOptions", "Details", "RestartRun", "ReturnToMenu" })
                Assert.That(FindButton(name).transform.IsChildOf(toolbar), Is.True);
            Canvas.ForceUpdateCanvases();
            var before = RectTransformUtility.CalculateRelativeRectTransformBounds(toolbar.parent, toolbar);
            controller.AdvanceRunMilliseconds(500000);
            controller.ToggleProductionMode();
            yield return null;
            scroll.verticalNormalizedPosition = 0;
            EventSystem.current.SetSelectedGameObject(FindButton("ReturnToMenu").gameObject);
            controller.Refresh();
            Canvas.ForceUpdateCanvases();
            var after = RectTransformUtility.CalculateRelativeRectTransformBounds(toolbar.parent, toolbar);
            Assert.That(Vector3.Distance(before.center, after.center), Is.LessThan(1f));
            var parentRect = ((RectTransform)toolbar.parent).rect;
            Assert.That(after.center.x, Is.GreaterThan(parentRect.center.x));
            Assert.That(after.center.y, Is.LessThan(parentRect.center.y));
            Assert.That(after.min.y, Is.GreaterThanOrEqualTo(parentRect.yMin - 1));
            Assert.That(after.max.x, Is.LessThanOrEqualTo(parentRect.xMax + 1));
            foreach (var name in new[] { "GameOptions", "Details", "RestartRun", "ReturnToMenu" })
                AssertSelectableReceivesPointer(FindButton(name));
            ClickButton("Details");
            Assert.That(controller.ShowDetails, Is.True);
            ClickButton("RestartRun");
            Assert.That(controller.Session.State, Is.EqualTo(RunState.Running));
            Assert.That(controller.Session.ElapsedMilliseconds, Is.Zero);
            yield return null;
            ClickButton("ReturnToMenu");
            Assert.That(controller.CurrentScreen, Is.EqualTo("MainMenu"));
            Assert.That(controller.Session, Is.Null);
        }

        [UnityTest]
        public IEnumerator HistoricalCountdownPbSurvivesImmediateRunCategoryAndReload()
        {
            var historical = new RunDefinition("speedrun-06-v1", 2000 * Economy.Scale, 3000);
            controller.StartRun(historical);
            controller.AdvanceRunMilliseconds(2003000);
            Assert.That(controller.Profile.FindBest(historical.CategoryKey), Is.Not.Null);
            Begin();
            Assert.That(controller.Session.Definition.RulesetId, Is.EqualTo("speedrun-06-v2"));
            Assert.That(controller.Session.Definition.CategoryKey, Is.Not.EqualTo(historical.CategoryKey));
            controller.AdvanceRunMilliseconds(2000000);
            string currentCategory = controller.Session.Definition.CategoryKey;
            Assert.That(controller.Profile.PersonalBests.Count, Is.EqualTo(2));
            Object.Destroy(root);
            yield return null;
            CreateController();
            yield return null;
            Assert.That(controller.Profile.FindBest(historical.CategoryKey), Is.Not.Null);
            Assert.That(controller.Profile.FindBest(currentCategory), Is.Not.Null);
            ClickButton("Statistics");
            Assert.That(FindText("PersonalStatistics").text, Does.Contain("speedrun-06-v1"));
            Assert.That(FindText("PersonalStatistics").text, Does.Contain("speedrun-06-v2"));
        }

        private bool HasButton(string name) => root.GetComponentsInChildren<Button>().Any(value => value.name == name);
        private Button FindButton(string name) => root.GetComponentsInChildren<Button>().Single(value => value.name == name);
        private Text FindText(string name) => root.GetComponentsInChildren<Text>().Single(value => value.name == name);

        private void ScrollTo(string name)
        {
            Canvas.ForceUpdateCanvases();
            var scroll = root.GetComponentsInChildren<ScrollRect>().Single();
            var button = FindButton(name);
            var target = (RectTransform)button.transform;
            var point = scroll.content.InverseTransformPoint(target.TransformPoint(target.rect.center));
            float targetOffset = -point.y - scroll.viewport.rect.height / 2;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(targetOffset, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
            scroll.content.anchoredPosition = position;
            Canvas.ForceUpdateCanvases();
        }

        private void ClickButton(string name) => ClickSelectable(FindButton(name));

        private RaycastResult AssertSelectableReceivesPointer(Selectable selectable)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(selectable.IsInteractable(), Is.True, selectable.name + " must be enabled");
            var rect = (RectTransform)selectable.transform;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), selectable.name + " must receive pointer hits at " + point);
            Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(), Is.SameAs(selectable), selectable.name + " must be unobstructed");
            return hits[0];
        }

        private void ClickSelectable(Selectable selectable)
        {
            var hit = AssertSelectableReceivesPointer(selectable);
            var pointer = new PointerEventData(EventSystem.current) { position = hit.screenPosition, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
