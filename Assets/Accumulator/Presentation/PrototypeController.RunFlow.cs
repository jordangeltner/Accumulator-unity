using System;
using System.Globalization;
using System.IO;
using System.Text;
using Accumulator.Core;
using Accumulator.Presentation.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Accumulator.Presentation
{
    // Local run navigation and storage adapter. All gameplay mutation belongs to RunSession.
    public sealed partial class PrototypeController
    {
        public string CurrentScreen { get; private set; } = "MainMenu";
        public string ProfileStatus { get; private set; } = "";
        public LocalProfile Profile => profile;
        public RunDefinition DefaultRunDefinition => new RunDefinition(RunDefinition.Default.RulesetId,
            RunDefinition.Default.TargetXp, RunDefinition.Default.CountdownMilliseconds,
            enableAlternateConversion, enableTimingProduction);

        private LocalProfile profile;
        private LocalProfileStore profileStore;
        private Transform uiCanvas;
        private GameObject gameplayRoot, mainMenuRoot, newRunRoot, runOptionsRoot, helpRoot, optionsRoot, statisticsRoot, resultsRoot;
        private UnityEngine.UI.Text menuBest, profileStatusText, runRules, runStatus, runHeading;
        private UnityEngine.UI.Text statisticsText, resultText, optionsStatus, volumeLabel, displayLabel, qualityLabel, detailsLabel;
        private UnityEngine.UI.Dropdown modeDropdown;
        private UnityEngine.UI.Button closeOptionsButton;
        private bool selectedPractice;
        private string optionsReturnScreen = "MainMenu";
        private RunResult handledResult;
        private bool resultWasNewBest;
        private double pendingFrameMilliseconds;
        private GameObject lastScrollSelection;

        /// <summary>Dependency injection for isolated profiles; only available outside a live run.</summary>
        public void UseLocalProfileStore(LocalProfileStore store)
        {
            if (Session != null && Session.State != RunState.Finished && Session.State != RunState.Abandoned)
                throw new InvalidOperationException("Return to the main menu before changing profiles.");
            profileStore = store ?? throw new ArgumentNullException(nameof(store));
            if (uiCanvas != null) { InitializeLocalProfile(); Refresh(); }
        }

        private void InitializeLocalProfile()
        {
            if (profileStore == null)
                profileStore = new LocalProfileStore(Path.Combine(Application.persistentDataPath, "profile-06.json"));
            profile = profileStore.Load(out string status);
            ProfileStatus = status;
            ShowDetails = profile.Options.showDetails;
            ApplyOptions();
        }

        private void SaveLocalProfile()
        {
            bool saved = profileStore.Save(profile, out string status);
            ProfileStatus = saved ? "Local PBs and options saved." : status;
        }

        private void ApplyOptions()
        {
            profile.Options.Validate();
            AudioListener.volume = profile.Options.masterVolume;
            int quality = profile.Options.qualityLevel;
            if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(quality, false);
            // Display preferences apply to standalone clients, not the user's Editor window.
            if (!Application.isEditor)
                Screen.SetResolution(profile.Options.width, profile.Options.height, profile.Options.fullScreen);
        }

        public void StartRun(bool practice = false) => StartRun(DefaultRunDefinition, practice);

        public void StartRun(RunDefinition definition, bool practice = false)
        {
            Session?.Abandon();
            Session = new RunSession(definition, practice);
            selectedPractice = practice;
            handledResult = null;
            resultWasNewBest = false;
            pendingFrameMilliseconds = 0;
            timingResult = "";
            Feedback = "";
            ShowScreen("Gameplay");
            Refresh();
        }

        public void RestartRun() => StartRun(Session?.Definition ?? DefaultRunDefinition, selectedPractice);

        public void ReturnToMainMenu()
        {
            Session?.Abandon();
            Session = null;
            pendingFrameMilliseconds = 0;
            Feedback = "";
            ShowScreen("MainMenu");
            Refresh();
        }

        private void UpdateRunFlow()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (CurrentScreen == "Gameplay") OpenOptions();
                else if (CurrentScreen == "Options") CloseOptions();
                else if (CurrentScreen == "RunOptions") CloseRunOptions();
                else if (CurrentScreen == "HowToPlay" || CurrentScreen == "Statistics") { ShowScreen("MainMenu"); Refresh(); }
            }
            if (CurrentScreen == "Gameplay" && Session != null
                && (Session.State == RunState.Countdown || Session.State == RunState.Running))
            {
                pendingFrameMilliseconds += Time.unscaledDeltaTime * 1000d;
                if (pendingFrameMilliseconds > RunSession.MaxAdvanceMilliseconds)
                {
                    Session.Pause("Run interrupted for more than an hour; practice only.");
                    pendingFrameMilliseconds = 0;
                }
                else
                {
                    int milliseconds = (int)Math.Floor(pendingFrameMilliseconds);
                    pendingFrameMilliseconds -= milliseconds;
                    AdvanceRunMilliseconds(milliseconds);
                }
            }
            Refresh();
        }

        /// <summary>Local explicit-time adapter, also used by deterministic integration checks.</summary>
        public void AdvanceRunMilliseconds(int milliseconds)
        {
            if (Session == null) return;
            try { Session.AdvanceMilliseconds(milliseconds); }
            catch (OverflowException)
            {
                Session.Pause("Numeric storage limit reached; practice only.");
                Feedback = "Run paused: numeric storage limit reached.";
                pendingFrameMilliseconds = 0;
            }
            HandleFinishedRun();
            Refresh();
        }

        private void HandleFinishedRun()
        {
            if (Session?.Result == null || ReferenceEquals(handledResult, Session.Result)) return;
            handledResult = Session.Result;
            resultWasNewBest = profile.TryRecord(handledResult);
            if (resultWasNewBest) SaveLocalProfile();
            pendingFrameMilliseconds = 0;
            ShowScreen("Results");
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) InterruptRun("Focus lost; practice only.");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) InterruptRun("Application suspended; practice only.");
        }

        private void InterruptRun(string reason)
        {
            if (Session == null) return;
            Session.Pause(reason);
            pendingFrameMilliseconds = 0;
            if (Paused && CurrentScreen == "Gameplay") OpenOptions();
            Refresh();
        }

        public void OpenOptions()
        {
            if (CurrentScreen == "Options") return;
            optionsReturnScreen = CurrentScreen;
            Session?.Pause("Options opened; practice only.");
            pendingFrameMilliseconds = 0;
            ShowScreen("Options");
            Refresh();
        }

        public void CloseOptions()
        {
            if (optionsReturnScreen == "Gameplay" && Paused)
            {
                Session.Resume();
                pendingFrameMilliseconds = 0;
            }
            ShowScreen(optionsReturnScreen);
            Refresh();
        }

        public void OpenRunOptions()
        {
            modeDropdown.Hide();
            ShowScreen("RunOptions");
            Refresh();
        }

        public void CloseRunOptions()
        {
            ShowScreen("NewRun");
            Refresh();
        }

        private void ChangeOption(Action<GameOptions> change)
        {
            change(profile.Options);
            ApplyOptions();
            SaveLocalProfile();
            Refresh();
        }

        private void CycleResolution()
        {
            ChangeOption(options =>
            {
                if (options.width == 1280 && options.height == 720) { options.width = 1600; options.height = 900; }
                else if (options.width == 1600 && options.height == 900) { options.width = 1920; options.height = 1080; }
                else { options.width = 1280; options.height = 720; }
            });
        }

        private void CycleQuality()
        {
            if (QualitySettings.names.Length == 0) return;
            ChangeOption(options => options.qualityLevel = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length);
        }

        private void ShowScreen(string screen)
        {
            CurrentScreen = screen;
            lastScrollSelection = null;
            gameplayRoot.SetActive(screen == "Gameplay");
            mainMenuRoot.SetActive(screen == "MainMenu" || screen == "NewRun" || screen == "RunOptions");
            var menuInteraction = mainMenuRoot.GetComponent<CanvasGroup>();
            menuInteraction.interactable = menuInteraction.blocksRaycasts = screen == "MainMenu";
            newRunRoot.SetActive(screen == "NewRun" || screen == "RunOptions");
            var runInteraction = newRunRoot.GetComponent<CanvasGroup>();
            runInteraction.interactable = runInteraction.blocksRaycasts = screen == "NewRun";
            runOptionsRoot.SetActive(screen == "RunOptions");
            helpRoot.SetActive(screen == "HowToPlay");
            optionsRoot.SetActive(screen == "Options");
            statisticsRoot.SetActive(screen == "Statistics");
            resultsRoot.SetActive(screen == "Results");
            var root = screen == "RunOptions" ? runOptionsRoot : screen == "NewRun" ? newRunRoot : screen == "HowToPlay" ? helpRoot
                : screen == "Options" ? optionsRoot : screen == "Statistics" ? statisticsRoot
                : screen == "Results" ? resultsRoot : screen == "Gameplay" ? gameplayRoot : mainMenuRoot;
            var first = Array.Find(root.GetComponentsInChildren<UnityEngine.UI.Selectable>(), control => control.IsInteractable());
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        private void KeepSelectedControlVisible()
        {
            if (CurrentScreen != "Gameplay" || EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || selected == lastScrollSelection) return;
            lastScrollSelection = selected;
            var scroll = selected.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            var selectedRect = selected.GetComponent<RectTransform>();
            if (scroll == null || selectedRect == null) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selectedRect);
            var viewport = scroll.viewport.rect;
            float adjustment = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y
                : bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0;
            if (Mathf.Approximately(adjustment, 0)) return;
            scroll.StopMovement();
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + adjustment, 0, Mathf.Max(0, scroll.content.rect.height - viewport.height));
            scroll.content.anchoredPosition = position;
        }

        private void RefreshRunFlow()
        {
            if (mainMenuRoot == null) return;
            var best = profile.FindBest(DefaultRunDefinition.CategoryKey);
            menuBest.text = "2,000 XP  |  Personal best: " + (best == null ? "No completed standard run yet" : FormatTime(best.elapsedMilliseconds));
            profileStatusText.text = ProfileStatus;
            runRules.text = "Reach 2,000 XP as quickly as possible.\n"
                + (selectedPractice ? "Practice runs do not update personal bests." : "Standard runs can set a local personal best.");
            modeDropdown.SetValueWithoutNotify(selectedPractice ? 1 : 0);
            int quality = QualitySettings.GetQualityLevel();
            volumeLabel.text = "Master volume: " + Mathf.RoundToInt(profile.Options.masterVolume * 100) + "%";
            displayLabel.text = $"{profile.Options.width} x {profile.Options.height}  |  {(profile.Options.fullScreen ? "Fullscreen" : "Windowed")}";
            qualityLabel.text = "Graphics quality: " + (quality < QualitySettings.names.Length ? QualitySettings.names[quality] : "Default");
            detailsLabel.text = "Economy details by default: " + (ShowDetails ? "On" : "Off");
            optionsStatus.text = ProfileStatus + (Application.isEditor ? "\nDisplay size applies in standalone builds." : "");
            closeOptionsButton.GetComponentInChildren<UnityEngine.UI.Text>().text = optionsReturnScreen == "Gameplay" ? "Back to Run" : "Back";
            if (CurrentScreen == "Statistics") RefreshStatistics();
            if (CurrentScreen == "Results" && handledResult != null) RefreshResult();
            if (Session == null) return;
            bool countdown = Session.State == RunState.Countdown;
            runHeading.text = countdown ? "STARTING IN " + Math.Ceiling(Session.CountdownRemainingMilliseconds / 1000d).ToString(CultureInfo.InvariantCulture)
                : $"TARGET {Session.Definition.TargetXp / Economy.Scale:N0} XP  |  {FormatTime(Session.ElapsedMilliseconds)}";
            runStatus.text = Session.IsPersonalBestEligible ? "Standard run — eligible for a local PB"
                : "Practice / PB ineligible: " + Session.EligibilityReason;
            if (Paused) runHeading.text += "  |  PAUSED";
        }

        private void RefreshStatistics()
        {
            var text = new StringBuilder("PERSONAL BESTS\n\n");
            var records = profile.PersonalBests;
            if (records.Count == 0) text.Append("Complete an uninterrupted standard run to set your first PB.\n");
            // Preserve historical entries while keeping this initial screen bounded.
            for (int index = 0; index < Math.Min(records.Count, 8); index++)
            {
                var best = records[index];
                text.AppendFormat(CultureInfo.InvariantCulture, "{0:N0} XP — {1}\n{2}\n\n", best.targetXp / (decimal)Economy.Scale,
                    FormatTime(best.elapsedMilliseconds), best.rulesetId);
            }
            if (records.Count > 8) text.Append("Additional historical PBs are retained locally.\n");
            text.Append("\nRun history and detailed analysis arrive in 0.8.\nGlobal leaderboards arrive in 0.10.\nAll current records are local and unverified.");
            statisticsText.text = text.ToString();
        }

        private void RefreshResult()
        {
            var result = handledResult;
            var best = profile.FindBest(result.CategoryKey);
            resultText.text = (resultWasNewBest ? "NEW PERSONAL BEST!\n\n" : "RUN COMPLETE\n\n")
                + "Time: " + FormatTime(result.ElapsedMilliseconds)
                + $"\nTarget reached: {result.TargetXp / Economy.Scale:N0} XP"
                + "\n" + (result.IsPersonalBestEligible ? "Standard — local PB eligible" : "Practice / PB ineligible: " + result.EligibilityReason)
                + "\nPersonal best: " + (best == null ? "None for this ruleset" : FormatTime(best.elapsedMilliseconds))
                + $"\n\nTiming: {result.NormalHits} normal / {result.CriticalHits} critical / {result.Misses} misses"
                + $"\nAttempts: {result.Attempts}  |  Skipped sweeps: {result.SkippedOpportunities}"
                + (Session.RecordingTruncated ? "\nDetailed recording reached its storage limit." : "")
                + "\n\n" + ProfileStatus;
        }

        public static string FormatTime(long milliseconds)
        {
            return (milliseconds / 60000).ToString("00", CultureInfo.InvariantCulture) + ":"
                + ((milliseconds / 1000) % 60).ToString("00", CultureInfo.InvariantCulture) + "."
                + ((milliseconds % 1000) / 100).ToString(CultureInfo.InvariantCulture);
        }

        private Transform MakeScreen(string name, string title, out GameObject root, bool dialog = false)
        {
            root = Node(name, uiCanvas, typeof(UnityEngine.UI.Image));
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.035f, 0.065f, 0.095f, dialog ? 0.94f : 1f);
            var content = Node(name + "Content", root.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
            Stretch(content.GetComponent<RectTransform>(), new Vector2(0.14f, 0.06f), new Vector2(0.86f, 0.94f), Vector2.zero, Vector2.zero);
            content.GetComponent<UnityEngine.UI.Image>().color = new Color(0.065f, 0.11f, 0.15f);
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 24, 24);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var heading = Label(name + "Title", content.transform, title, 34);
            heading.color = accent;
            Height(heading.gameObject, 55);
            root.SetActive(false);
            return content.transform;
        }

        private UnityEngine.UI.Text Paragraph(string name, Transform parent, string text, float height)
        {
            var label = Label(name, parent, text, 22);
            label.alignment = TextAnchor.UpperLeft;
            label.supportRichText = false;
            Height(label.gameObject, height);
            return label;
        }

        private void HelpSection(string name, Transform parent, string text, float height)
        {
            Paragraph(name, parent, text, height).supportRichText = true;
        }

        private UnityEngine.UI.Dropdown BuildRunModeDropdown(Transform parent)
        {
            var holder = Node("RunMode", parent, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Dropdown));
            Height(holder, 48);
            holder.GetComponent<UnityEngine.UI.Image>().color = new Color(0.18f, 0.38f, 0.46f);
            var dropdown = holder.GetComponent<UnityEngine.UI.Dropdown>();
            dropdown.targetGraphic = holder.GetComponent<UnityEngine.UI.Image>();
            var caption = Label("ModeCaption", holder.transform, "Standard", 22);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-48, -4));
            var arrow = Label("DropdownArrow", holder.transform, "v", 22);
            arrow.alignment = TextAnchor.MiddleCenter;
            Stretch(arrow.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-44, 0), Vector2.zero);

            var template = Node("Template", holder.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect));
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = Vector2.zero;
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.sizeDelta = new Vector2(0, 96);
            templateRect.anchoredPosition = new Vector2(0, -2);
            template.GetComponent<UnityEngine.UI.Image>().color = new Color(0.09f, 0.17f, 0.22f);
            var viewport = Node("Viewport", template.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.RectMask2D));
            viewport.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            Stretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var content = Node("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 44);
            var item = Node("Item", content.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Toggle));
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 1);
            itemRect.anchorMax = Vector2.one;
            itemRect.pivot = new Vector2(0.5f, 1);
            itemRect.sizeDelta = new Vector2(0, 44);
            item.GetComponent<UnityEngine.UI.Image>().color = new Color(0.18f, 0.38f, 0.46f);
            var checkmark = Node("Checkmark", item.transform, typeof(UnityEngine.UI.Image));
            checkmark.GetComponent<UnityEngine.UI.Image>().color = accent;
            checkmark.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(0, 0.5f);
            checkRect.sizeDelta = new Vector2(14, 14);
            checkRect.anchoredPosition = new Vector2(20, 0);
            var itemLabel = Label("ItemLabel", item.transform, "", 22);
            Stretch(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(40, 0), new Vector2(-12, 0));
            var toggle = item.GetComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = item.GetComponent<UnityEngine.UI.Image>();
            toggle.graphic = checkmark.GetComponent<UnityEngine.UI.Image>();
            var scroll = template.GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("Standard"));
            dropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("Practice"));
            dropdown.onValueChanged.AddListener(value => { selectedPractice = value == 1; Refresh(); });
            template.SetActive(false);
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private void BuildRunFlowInterface()
        {
            var main = MakeScreen("MainMenu", "ACCUMULATOR — SPEEDRUN", out mainMenuRoot);
            mainMenuRoot.AddComponent<CanvasGroup>();
            Paragraph("MenuIntroduction", main, "Route your economy. Master the timing. Beat your best.", 42);
            Button("NewRun", main, "New Run", () => { ShowScreen("NewRun"); Refresh(); });
            Button("HowToPlay", main, "How to Play", () => ShowScreen("HowToPlay"));
            Button("Options", main, "Options", OpenOptions);
            Button("Statistics", main, "Statistics", () => { ShowScreen("Statistics"); Refresh(); });
            menuBest = Paragraph("MenuBest", main, "", 70);
            profileStatusText = Paragraph("ProfileStatus", main, "", 90);

            var dialog = MakeScreen("NewRunDialog", "NEW RUN", out newRunRoot, true);
            newRunRoot.AddComponent<CanvasGroup>();
            runRules = Paragraph("RunRules", dialog, "", 85);
            Paragraph("RunModeLabel", dialog, "Mode", 30);
            modeDropdown = BuildRunModeDropdown(dialog);
            Button("RunOptions", dialog, "Run Options", OpenRunOptions);
            Button("BeginRun", dialog, "Begin Run", () => StartRun(selectedPractice));
            Button("CancelNewRun", dialog, "Cancel", () => ShowScreen("MainMenu"));

            var runOptions = MakeScreen("RunOptionsDialog", "RUN OPTIONS", out runOptionsRoot, true);
            Paragraph("RunConfigurationPlaceholder", runOptions, "Run customization is coming later.\n\nCurrent setup\n• Target: 2,000 XP\n• Starting capacity: 6\n• Critical range: Standard\n\nOpening game Options or losing focus makes an attempt ineligible for personal bests.", 280);
            Button("CloseRunOptions", runOptions, "Back", CloseRunOptions);

            var help = MakeScreen("HowToPlayScreen", "HOW TO PLAY", out helpRoot);
            HelpSection("HowToPlayGoal", help, "<b>Goal</b>\n• Reach 2,000 XP as fast as possible.\n• Retry to beat your personal best in Standard mode.", 95);
            HelpSection("HowToPlayEconomy", help, "<b>Economy</b>\n• Allocate capacity: Production makes A, Conversion turns A into B, and Development turns B into Credits and XP.", 95);
            HelpSection("HowToPlayUpgrades", help, "<b>Upgrades</b>\n• Spend Credits on efficiency and capacity; rebalance to ease bottlenecks.\n• Fast Conversion unlocks at 100 XP; Timing Production at 500 XP.", 115);
            HelpSection("HowToPlayTiming", help, "<b>Timing</b>\n• Click once per sweep: success earns A, critical earns more, and misses earn nothing.", 95);
            Button("CloseHowToPlay", help, "Back", () => ShowScreen("MainMenu"));

            var options = MakeScreen("OptionsScreen", "OPTIONS", out optionsRoot);
            var volumeRow = Row("VolumeRow", options, 48);
            volumeLabel = Label("VolumeLabel", volumeRow.transform, ""); Flexible(volumeLabel);
            Button("VolumeDown", volumeRow.transform, "-", () => ChangeOption(value => value.masterVolume -= 0.1f), 70);
            Button("VolumeUp", volumeRow.transform, "+", () => ChangeOption(value => value.masterVolume += 0.1f), 70);
            displayLabel = Paragraph("DisplayLabel", options, "", 36);
            var displayRow = Row("DisplayRow", options, 48);
            Button("Resolution", displayRow.transform, "Resolution", CycleResolution, 240);
            Button("Fullscreen", displayRow.transform, "Toggle fullscreen", () => ChangeOption(value => value.fullScreen = !value.fullScreen), 260);
            qualityLabel = Paragraph("QualityLabel", options, "", 36);
            Button("Quality", options, "Change graphics quality", CycleQuality);
            detailsLabel = Paragraph("DetailsLabel", options, "", 36);
            Button("DefaultDetails", options, "Toggle economy details", ToggleDetails);
            Paragraph("InputOutputHelp", options, "Controls: mouse, or keyboard navigation (arrows and Enter). Escape opens Options during a run. Input/output devices follow your system settings. Key remapping arrives in 0.7; device selection and richer options are deferred.", 100);
            optionsStatus = Paragraph("OptionsStatus", options, "", 80);
            closeOptionsButton = Button("CloseOptions", options, "Back", CloseOptions);

            var statistics = MakeScreen("StatisticsScreen", "STATISTICS", out statisticsRoot);
            statisticsText = Paragraph("PersonalStatistics", statistics, "", 590);
            Button("CloseStatistics", statistics, "Back", () => ShowScreen("MainMenu"));

            var results = MakeScreen("ResultsScreen", "RUN OVERVIEW", out resultsRoot);
            resultText = Paragraph("RunResult", results, "", 430);
            Button("QuickRetry", results, "Retry same run", RestartRun);
            Button("ResultsOptions", results, "Options", OpenOptions);
            Button("ResultsMenu", results, "Main Menu", ReturnToMainMenu);
            ShowScreen("MainMenu");
        }
    }
}
