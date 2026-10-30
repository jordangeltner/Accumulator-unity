using System;
using System.Globalization;
using Accumulator.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Accumulator.Presentation
{
    /// <summary>Local prototype adapter. Frame time schedules explicit core ticks; it is not trusted server time.</summary>
    public sealed class PrototypeController : MonoBehaviour
    {
        [SerializeField, Tooltip("Enable the 0.2 Standard/Fast Conversion experiment. Disable before Play for the 0.1 baseline.")]
        private bool enableAlternateConversion = true;
        [SerializeField, Tooltip("Enable the 0.3 optional timing game in the Production slot.")]
        private bool enableTimingProduction = true;
        public Economy Simulation { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
        public string Feedback { get; private set; } = "";
        public bool ShowDetails { get; private set; }
        private double pendingSeconds;
        private double pendingTimingMilliseconds;
        private Font font;
        private Text balances, netRates, capacity, feedbackText, clock;
        private UnityEngine.UI.Text nextUnlock;
        private readonly Text[] allocations = new Text[3], rates = new Text[3], upgradeLabels = new Text[3], upgradeEffects = new Text[3];
        private readonly Button[] plus = new Button[3], minus = new Button[3], buy = new Button[3];
        private Button pauseButton, expandCapacity, detailsButton;
        private UnityEngine.UI.Button conversionModeButton;
        private UnityEngine.UI.Text conversionModeDetails;
        private UnityEngine.UI.Button productionModeButton, timingBarButton, speed5Button, speed20Button;
        private UnityEngine.UI.Text productionModeDetails, timingStatus;
        private RectTransform timingMarker;
        private GameObject timingPanel;
        private GameObject productionModeRow, conversionModeRow;
        private LayoutElement productionCardHeight, conversionCardHeight;
        private string timingResult = "";
        private readonly Color ink = new Color(0.88f, 0.93f, 0.98f);
        private readonly Color muted = new Color(0.65f, 0.75f, 0.84f);
        private readonly Color accent = new Color(0.35f, 0.86f, 0.73f);
        private readonly Color warning = new Color(1f, 0.72f, 0.35f);

        private void Awake()
        {
            Simulation = new Economy(enableAlternateConversion, enableTimingProduction);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            Refresh();
        }

        private void Update()
        {
            if (!Paused)
            {
                try
                {
                    if (Simulation.SelectedProductionMode == ProductionMode.Timing)
                    {
                        pendingTimingMilliseconds += Time.unscaledDeltaTime * 1000d;
                        int milliseconds = (int)Math.Min(int.MaxValue, Math.Floor(pendingTimingMilliseconds));
                        Simulation.AdvanceTimingMilliseconds(milliseconds);
                        pendingTimingMilliseconds -= milliseconds;
                    }
                    pendingSeconds += Time.unscaledDeltaTime * Speed;
                    // Keep fractional elapsed time and backlog; never use device-clock/offline time.
                    int ticks = (int)Math.Min(500, Math.Floor((pendingSeconds + 1e-9) * Economy.TicksPerSecond));
                    if (ticks > 0)
                    {
                        Simulation.AdvanceTicks(ticks);
                        pendingSeconds -= ticks / (double)Economy.TicksPerSecond;
                    }
                }
                catch (OverflowException)
                {
                    Paused = true;
                    Feedback = "Simulation paused: numeric storage limit reached.";
                }
            }
            Refresh();
        }

        public void ChangeAllocation(int index, int delta)
        {
            if (index < 0 || index > 2) return;
            Feedback = Simulation.TrySetAllocation(index, Simulation.Allocation(index) + delta, out string reason)
                ? "" : reason;
            Refresh();
        }

        public void PurchaseUpgrade(int index)
        {
            Feedback = Simulation.TryPurchase(index, out string reason)
                ? "" : reason;
            Refresh();
        }

        public void TogglePause() { Paused = !Paused; Refresh(); }
        public void SetSpeed(int speed)
        {
            if (Simulation.SelectedProductionMode == ProductionMode.Timing && speed != 1)
                Feedback = "Timing production runs at 1x.";
            else if (speed == 1 || speed == 5 || speed == 20) Speed = speed;
            Refresh();
        }

        public void PurchaseCapacity()
        {
            Feedback = Simulation.TryExpandCapacity(out string reason) ? "" : reason;
            Refresh();
        }

        public void ToggleConversionMode()
        {
            var next = Simulation.SelectedConversionMode == ConversionMode.Standard ? ConversionMode.Fast : ConversionMode.Standard;
            Feedback = Simulation.TrySetConversionMode(next, out string reason)
                ? "" : reason;
            Refresh();
        }

        public void ToggleProductionMode()
        {
            var next = Simulation.SelectedProductionMode == ProductionMode.Passive ? ProductionMode.Timing : ProductionMode.Passive;
            if (Simulation.TrySetProductionMode(next, out string reason))
            {
                if (next == ProductionMode.Timing) Speed = 1;
                Feedback = "";
            }
            else Feedback = reason;
            Refresh();
        }

        public void StrikeTiming()
        {
            if (Paused) { Feedback = "Resume to play the timing game."; Refresh(); return; }
            try
            {
                if (Simulation.TryStrikeTiming(out bool hit, out long reward, out string reason))
                {
                    timingResult = hit
                        ? (Simulation.LastTimingResult == TimingResult.Critical ? "CRITICAL! +" : "SUCCESS! +") + Number(reward) + " A"
                        : "MISS — no A";
                    Feedback = "";
                }
                else Feedback = reason;
            }
            catch (OverflowException)
            {
                Paused = true;
                Feedback = "Simulation paused: numeric storage limit reached.";
            }
            Refresh();
        }

        public void ToggleDetails() { ShowDetails = !ShowDetails; Refresh(); }

        private static string Number(long raw) => (raw / (decimal)Economy.Scale).ToString("N2", CultureInfo.InvariantCulture);
        private static string PerSecond(long raw) => (raw * (decimal)Economy.TicksPerSecond / Economy.Scale).ToString("0.00", CultureInfo.InvariantCulture);
        private static string SignedRate(long raw) => (raw * (decimal)Economy.TicksPerSecond / Economy.Scale).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
        private static string A(string text) => $"<color={PresentationLabels.ResourceAColor}>{text}</color>";
        private static string B(string text) => $"<color={PresentationLabels.ResourceBColor}>{text}</color>";
        private static string Credit(string text) => $"<color={PresentationLabels.CreditsColor}>{text}</color>";
        private static string Xp(string text) => $"<color={PresentationLabels.XpColor}>{text}</color>";

        private void RefreshPrice(Button button, bool hasCost, long cost, string prefix = "", string unavailable = "Unavailable")
        {
            button.interactable = hasCost && Simulation.Credits >= cost;
            string caption = hasCost ? prefix + (cost / Economy.Scale).ToString("N0", CultureInfo.InvariantCulture) + " Credits" : unavailable;
            var label = button.GetComponentInChildren<Text>();
            label.text = button.interactable ? Credit(caption) : caption;
            label.color = button.interactable ? ink : muted;
            button.GetComponent<Outline>().effectColor = button.interactable
                ? new Color(0.42f, 0.66f, 0.74f) : new Color(0.25f, 0.28f, 0.31f);
        }

        public void Refresh()
        {
            if (balances == null) return;
            var flow = Simulation.Preview();
            int used = Simulation.Allocation(0) + Simulation.Allocation(1) + Simulation.Allocation(2);
            balances.text = A($"{PresentationLabels.ResourceA}   {Number(Simulation.A)}") + "     " + B($"{PresentationLabels.ResourceB}   {Number(Simulation.B)}") + "     " + Credit($"{PresentationLabels.Credits}   {Number(Simulation.Credits)}") + "     " + Xp($"{PresentationLabels.LifetimeXp}   {Number(Simulation.LifetimeXp)}");
            netRates.text = "NET /s     " + A($"A {SignedRate(flow.AProduced - flow.AConsumed)}") + "     " + B($"B {SignedRate(flow.BProduced - flow.BConsumed)}") + "     " + Credit($"Credits {SignedRate(flow.CreditsProduced)}");
            capacity.text = $"CAPACITY   {used} / {Simulation.Capacity}";
            RefreshNextUnlock();
            bool hasExpansion = Simulation.TryGetExpansionCost(out long expansionCost);
            RefreshPrice(expandCapacity, hasExpansion, expansionCost, "+1  |  ", "Max capacity");
            clock.text = $"{Simulation.Ticks / 10m:0.0}s  |  {Speed}x";
            detailsButton.GetComponentInChildren<Text>().text = ShowDetails ? "Less detail" : "Details";
            for (int i = 0; i < 3; i++)
            {
                allocations[i].text = Simulation.Allocation(i).ToString();
                plus[i].interactable = used < Simulation.Capacity;
                minus[i].interactable = Simulation.Allocation(i) > 0;
                bool hasCost = Simulation.TryGetCost(i, out long cost);
                RefreshPrice(buy[i], hasCost, cost);
                upgradeLabels[i].text = $"Efficiency {Simulation.Level(i)}";
                string effect = PresentationLabels.Effects[i];
                if (i == Economy.AProducer && Simulation.SelectedProductionMode == ProductionMode.Timing)
                    effect = PresentationLabels.TimingProductionUpgradeEffect;
                if (i == Economy.Converter && Simulation.SelectedConversionMode == ConversionMode.Fast)
                    effect = PresentationLabels.FastConversionUpgradeEffect;
                if (i == 0) effect = A(effect);
                else if (i == 1) effect = B(effect);
                else effect = Credit(effect);
                upgradeEffects[i].text = effect;
            }
            RefreshTiming();
            if (conversionModeButton != null)
            {
                bool fast = Simulation.SelectedConversionMode == ConversionMode.Fast;
                bool unlocked = Simulation.FastConversionUnlocked;
                conversionModeRow.SetActive(unlocked);
                conversionCardHeight.minHeight = conversionCardHeight.preferredHeight = unlocked ? 192 : 142;
                conversionModeDetails.gameObject.SetActive(ShowDetails);
                conversionModeButton.interactable = Simulation.FastConversionUnlocked;
                conversionModeButton.GetComponentInChildren<Text>().text = "Mode: " + (fast ? PresentationLabels.FastConversion : PresentationLabels.StandardConversion);
                decimal output = (fast ? 1.5m : 1m) * (1m + Simulation.Level(Economy.Converter) / 2m);
                conversionModeDetails.text = A(fast ? "4 A" : "2 A") + " -> " + B(output.ToString("0.##", CultureInfo.InvariantCulture) + " B") + " / capacity /s";
            }
            rates[0].text = Simulation.SelectedProductionMode == ProductionMode.Timing
                ? A("A per hit")
                : A(ShowDetails ? $"A {PerSecond(flow.AProduced)} / {PerSecond(flow.MaxAProduced)}" : $"A {PerSecond(flow.AProduced)}/s");
            rates[1].text = B(ShowDetails ? $"B {PerSecond(flow.BProduced)} / {PerSecond(flow.MaxBProduced)}" : $"B {PerSecond(flow.BProduced)}/s")
                + (ShowDetails ? "\n" + A($"A input {PerSecond(flow.AConsumed)}/s") : "") + State(1, flow.ConversionStarved, "A");
            rates[2].text = ShowDetails
                ? Credit("Credits") + " + " + Xp("XP") + $" {PerSecond(flow.CreditsProduced)} / {PerSecond(flow.MaxCreditsProduced)}\n" + B($"B input {PerSecond(flow.BConsumed)}/s") + State(2, flow.DevelopmentStarved, "B")
                : $"{PerSecond(flow.CreditsProduced)} " + Credit("Credits") + " + " + Xp("XP") + "/s" + State(2, flow.DevelopmentStarved, "B");
            pauseButton.GetComponentInChildren<Text>().text = Paused ? "Resume" : "Pause";
            feedbackText.text = Feedback;
            feedbackText.gameObject.SetActive(Feedback.Length > 0);
        }

        private void RefreshNextUnlock()
        {
            bool fastPending = Simulation.AlternateConversionEnabled && !Simulation.FastConversionUnlocked;
            bool timingPending = Simulation.TimingProductionEnabled && !Simulation.TimingProductionUnlocked;
            nextUnlock.gameObject.SetActive(fastPending || timingPending);
            if (!fastPending && !timingPending) return;
            string name;
            long threshold;
            if (fastPending)
            {
                name = "Fast Conversion";
                threshold = Economy.FastConversionUnlockXp;
            }
            else
            {
                name = "Timing Production";
                threshold = Economy.TimingProductionUnlockXp;
            }
            nextUnlock.text = "Next: " + name + " at " + Xp(threshold + " XP");
        }

        private void RefreshTiming()
        {
            bool timing = Simulation.SelectedProductionMode == ProductionMode.Timing;
            speed5Button.interactable = speed20Button.interactable = !timing;
            if (productionModeButton == null) return;
            bool unlocked = Simulation.TimingProductionUnlocked;
            productionModeRow.SetActive(unlocked);
            productionModeDetails.gameObject.SetActive(ShowDetails);
            productionModeButton.interactable = Simulation.TimingProductionUnlocked;
            productionModeButton.GetComponentInChildren<Text>().text = "Mode: " + (timing ? PresentationLabels.TimingProduction : PresentationLabels.PassiveProduction);
            productionModeDetails.text = timing ? "Manual A production  |  1x" : "Automatic A production";
            if (timingPanel.activeSelf != timing) timingPanel.SetActive(timing);
            float height = timing ? 324 : unlocked ? 192 : 142;
            productionCardHeight.minHeight = productionCardHeight.preferredHeight = height;
            if (!timing) return;
            float position = Simulation.TimingPosition / (float)Economy.TimingPositionScale;
            timingMarker.anchorMin = new Vector2(position, 0);
            timingMarker.anchorMax = new Vector2(position, 1);
            timingMarker.anchoredPosition = Vector2.zero;
            bool hasWorkers = Simulation.Allocation(Economy.AProducer) > 0;
            timingBarButton.interactable = !Paused && hasWorkers && Simulation.TimingAttemptAvailable;
            decimal reward = Simulation.Allocation(Economy.AProducer) * (2m + Simulation.Level(Economy.AProducer)) * 2m;
            string state = Paused ? "Paused" : !hasWorkers ? "Assign Production capacity" : Simulation.TimingAttemptAvailable ? "Ready" : "Next sweep…";
            timingStatus.text = $"{state}  |  Success: {reward:0.##} A  |  Critical: {reward * 1.5m:0.##} A  |  Hits {Simulation.TimingHits}/{Simulation.TimingAttempts}";
            if (timingResult.Length > 0) timingStatus.text += "  |  Last: " + timingResult;
        }

        private string State(int index, bool starved, string input) => Simulation.Allocation(index) == 0 ? "   |   Unassigned" : starved ? "   |   Need " + (input == "A" ? A(input) : B(input)) : "";

        private GameObject Node(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (var type in components) go.AddComponent(type);
            return go;
        }
        private void Height(GameObject go, float height)
        {
            var layout = go.AddComponent<LayoutElement>(); layout.minHeight = height; layout.preferredHeight = height;
        }
        private Text Label(string name, Transform parent, string text, int size = 20)
        {
            var go = Node(name, parent, typeof(Text));
            var label = go.GetComponent<Text>(); label.font = font; label.fontSize = size;
            label.color = ink; label.text = text; label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false; label.supportRichText = true;
            return label;
        }
        private Button Button(string name, Transform parent, string text, UnityEngine.Events.UnityAction action, float width = 110)
        {
            var go = Node(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.GetComponent<Image>().color = new Color(0.18f, 0.38f, 0.46f);
            var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0.42f, 0.66f, 0.74f); outline.effectDistance = new Vector2(1, -1);
            var layout = go.GetComponent<LayoutElement>(); layout.minWidth = width; layout.preferredWidth = width; layout.minHeight = 42;
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(0.7f, 1f, 0.92f); colors.pressedColor = new Color(0.45f, 0.85f, 0.72f); colors.disabledColor = new Color(0.35f, 0.4f, 0.45f); button.colors = colors;
            button.onClick.AddListener(action);
            var label = Label("Label", go.transform, text); label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }
        private GameObject Row(string name, Transform parent, float height, bool panel = false)
        {
            var go = Node(name, parent, typeof(HorizontalLayoutGroup)); Height(go, height);
            var group = go.GetComponent<HorizontalLayoutGroup>(); group.spacing = 14; group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true; group.childControlHeight = true; group.childForceExpandWidth = false; group.childForceExpandHeight = false;
            if (panel) { var image = go.AddComponent<Image>(); image.color = new Color(0.095f, 0.15f, 0.20f); group.padding = new RectOffset(16, 16, 12, 12); }
            return go;
        }
        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        { rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = low; rt.offsetMax = high; }
        private void Flexible(Text label, float min = 0)
        { var le = label.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.minWidth = min; }

        private void StylePriceButton(Button button)
        {
            button.GetComponent<Image>().color = Color.white;
            var colors = button.colors;
            colors.normalColor = new Color(0.10f, 0.22f, 0.28f);
            colors.disabledColor = new Color(0.14f, 0.16f, 0.18f);
            colors.highlightedColor = new Color(0.16f, 0.34f, 0.42f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.12f, 0.30f, 0.34f);
            button.colors = colors;
            var price = button.GetComponentInChildren<Text>();
            price.resizeTextForBestFit = true; price.resizeTextMinSize = 14; price.resizeTextMaxSize = 20;
            Stretch(price.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
        }

        private void BuildInterface()
        {
            var canvas = Node("PrototypeCanvas", transform, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1500, 1000); scaler.matchWidthOrHeight = 1;
            var backdrop = Node("Background", canvas.transform, typeof(Image));
            Stretch(backdrop.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.GetComponent<Image>().color = new Color(0.035f, 0.065f, 0.095f);
            var scroll = Node("ManagementScroll", backdrop.transform, typeof(ScrollRect));
            Stretch(scroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(28, 18), new Vector2(-28, -18));
            var viewport = Node("Viewport", scroll.transform, typeof(Image), typeof(RectMask2D));
            viewport.GetComponent<Image>().color = Color.clear;
            Stretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var content = Node("Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = content.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = Vector2.zero;
            var vertical = content.GetComponent<VerticalLayoutGroup>(); vertical.spacing = 6; vertical.childControlHeight = true; vertical.childForceExpandHeight = false; vertical.childControlWidth = true; vertical.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrolling = scroll.GetComponent<ScrollRect>(); scrolling.viewport = viewport.GetComponent<RectTransform>(); scrolling.content = rect; scrolling.horizontal = false; scrolling.movementType = ScrollRect.MovementType.Clamped;
            var title = Label("Title", content.transform, "ACCUMULATOR", 30); title.color = accent; Height(title.gameObject, 44);
            balances = Label("Balances", content.transform, "", 25); Height(balances.gameObject, 48);
            balances.resizeTextForBestFit = true; balances.resizeTextMinSize = 18; balances.resizeTextMaxSize = 25;
            netRates = Label("NetRates", content.transform, "", 20); netRates.color = accent; Height(netRates.gameObject, 32);
            nextUnlock = Label("NextUnlock", content.transform, "", 19); Height(nextUnlock.gameObject, 32);
            var capacityRow = Row("CapacityRow", content.transform, 44);
            capacity = Label("Capacity", capacityRow.transform, "", 22); capacity.gameObject.AddComponent<LayoutElement>().preferredWidth = 360;
            expandCapacity = Button("ExpandCapacity", capacityRow.transform, "", PurchaseCapacity, 250);
            StylePriceButton(expandCapacity);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                bool showMode = i == Economy.Converter && Simulation.AlternateConversionEnabled;
                bool showTiming = i == Economy.AProducer && Simulation.TimingProductionEnabled;
                var card = Node("Process" + i, content.transform, typeof(Image), typeof(VerticalLayoutGroup)); Height(card, 142);
                if (showTiming) productionCardHeight = card.GetComponent<LayoutElement>();
                if (showMode) conversionCardHeight = card.GetComponent<LayoutElement>();
                card.GetComponent<Image>().color = new Color(0.095f, 0.15f, 0.20f);
                var layout = card.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(16, 16, 10, 10); layout.spacing = 6;
                layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
                var row = Row("ProductionRow", card.transform, 64);
                var name = Label("Name", row.transform, PresentationLabels.Processes[i], 23); name.gameObject.AddComponent<LayoutElement>().preferredWidth = 220;
                var allocationControls = Row("AllocationControls", row.transform, 48);
                var controlLayout = allocationControls.GetComponent<LayoutElement>(); controlLayout.minWidth = 158; controlLayout.preferredWidth = 158;
                allocationControls.GetComponent<HorizontalLayoutGroup>().spacing = 4;
                minus[i] = Button("Decrease" + i, allocationControls.transform, "-", () => ChangeAllocation(index, -1), 56);
                allocations[i] = Label("Allocation", allocationControls.transform, "0", 24); allocations[i].alignment = TextAnchor.MiddleCenter; allocations[i].gameObject.AddComponent<LayoutElement>().preferredWidth = 38;
                plus[i] = Button("Increase" + i, allocationControls.transform, "+", () => ChangeAllocation(index, 1), 56);
                rates[i] = Label("Throughput", row.transform, "", 21); Flexible(rates[i], 500);
                if (showMode)
                {
                    var modeRow = Row("ConversionModeRow", card.transform, 44);
                    conversionModeRow = modeRow;
                    conversionModeButton = Button("ConversionMode", modeRow.transform, "", ToggleConversionMode, 220);
                    conversionModeDetails = Label("ConversionModeDetails", modeRow.transform, "", 19);
                    Flexible(conversionModeDetails);
                }
                if (showTiming)
                {
                    var modeRow = Row("ProductionModeRow", card.transform, 44);
                    productionModeRow = modeRow;
                    productionModeButton = Button("ProductionMode", modeRow.transform, "", ToggleProductionMode, 220);
                    productionModeDetails = Label("ProductionModeDetails", modeRow.transform, "", 19);
                    Flexible(productionModeDetails);
                    BuildTimingPanel(card.transform);
                }
                var upgradeRow = Row("Upgrade" + i, card.transform, 52);
                upgradeLabels[i] = Label("Description", upgradeRow.transform, "", 20); upgradeLabels[i].color = muted; upgradeLabels[i].gameObject.AddComponent<LayoutElement>().preferredWidth = 220;
                buy[i] = Button("Purchase" + i, upgradeRow.transform, "", () => PurchaseUpgrade(index), 250);
                StylePriceButton(buy[i]);
                upgradeEffects[i] = Label("UpgradeEffect" + i, upgradeRow.transform, "", 21); Flexible(upgradeEffects[i], 500);
            }
            feedbackText = Label("Feedback", content.transform, "", 20); feedbackText.color = warning; Height(feedbackText.gameObject, 36);
            var controls = Row("TestControls", content.transform, 44);
            pauseButton = Button("Pause", controls.transform, "Pause", TogglePause);
            Button("Speed1", controls.transform, "1x", () => SetSpeed(1), 70);
            speed5Button = Button("Speed5", controls.transform, "5x", () => SetSpeed(5), 70);
            speed20Button = Button("Speed20", controls.transform, "20x", () => SetSpeed(20), 70);
            detailsButton = Button("Details", controls.transform, "Details", ToggleDetails, 140);
            clock = Label("Clock", controls.transform, "", 19); Flexible(clock);
            var note = Label("LocalOnly", content.transform, "Not saved", 18); note.color = muted; Height(note.gameObject, 30);
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = Node("EventSystem", transform, typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            eventSystem.firstSelectedGameObject = minus[0].gameObject;
            eventSystem.SetSelectedGameObject(minus[0].gameObject);
        }

        private void BuildTimingPanel(Transform parent)
        {
            timingPanel = Node("TimingPanel", parent, typeof(VerticalLayoutGroup));
            Height(timingPanel, 126);
            var layout = timingPanel.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var instruction = Label("TimingInstructions", timingPanel.transform, "Click when the marker reaches a colored zone. One click per sweep.", 19);
            Height(instruction.gameObject, 28);
            var bar = Node("TimingBar", timingPanel.transform, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            Height(bar, 44);
            bar.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.23f, 0.30f);
            timingBarButton = bar.GetComponent<UnityEngine.UI.Button>();
            timingBarButton.targetGraphic = bar.GetComponent<UnityEngine.UI.Image>();
            timingBarButton.onClick.AddListener(StrikeTiming);
            var target = Node("TimingTarget", bar.transform, typeof(UnityEngine.UI.Image));
            target.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.46f, 0.34f);
            target.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Stretch(target.GetComponent<RectTransform>(), new Vector2(Economy.TimingTargetStart / (float)Economy.TimingPositionScale, 0), new Vector2(Economy.TimingTargetEnd / (float)Economy.TimingPositionScale, 1), new Vector2(0, 3), new Vector2(0, -3));
            // Label both exposed sides of success so the nested critical zone remains readable.
            float criticalLeft = (Economy.TimingCriticalStart - Economy.TimingTargetStart) / (float)(Economy.TimingTargetEnd - Economy.TimingTargetStart);
            float criticalRight = (Economy.TimingCriticalEnd - Economy.TimingTargetStart) / (float)(Economy.TimingTargetEnd - Economy.TimingTargetStart);
            var successLeft = Label("TimingSuccessLeft", target.transform, "SUCCESS", 15);
            successLeft.alignment = TextAnchor.MiddleCenter;
            Stretch(successLeft.rectTransform, Vector2.zero, new Vector2(criticalLeft, 1), Vector2.zero, Vector2.zero);
            var successRight = Label("TimingSuccessRight", target.transform, "SUCCESS", 15);
            successRight.alignment = TextAnchor.MiddleCenter;
            Stretch(successRight.rectTransform, new Vector2(criticalRight, 0), Vector2.one, Vector2.zero, Vector2.zero);
            var critical = Node("TimingCritical", bar.transform, typeof(UnityEngine.UI.Image));
            critical.GetComponent<UnityEngine.UI.Image>().color = new Color(0.76f, 0.46f, 0.10f);
            critical.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Stretch(critical.GetComponent<RectTransform>(), new Vector2(Economy.TimingCriticalStart / (float)Economy.TimingPositionScale, 0), new Vector2(Economy.TimingCriticalEnd / (float)Economy.TimingPositionScale, 1), new Vector2(0, 3), new Vector2(0, -3));
            var criticalLabel = Label("TimingCriticalLabel", critical.transform, "CRITICAL", 15);
            criticalLabel.alignment = TextAnchor.MiddleCenter;
            Stretch(criticalLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var marker = Node("TimingMarker", bar.transform, typeof(UnityEngine.UI.Image));
            marker.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            marker.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            timingMarker = marker.GetComponent<RectTransform>();
            timingMarker.anchorMin = Vector2.zero;
            timingMarker.anchorMax = new Vector2(0, 1);
            timingMarker.sizeDelta = new Vector2(6, -4);
            timingStatus = Label("TimingStatus", timingPanel.transform, "", 18);
            timingStatus.color = accent;
            Height(timingStatus.gameObject, 32);
            timingPanel.SetActive(false);
        }
    }
}
