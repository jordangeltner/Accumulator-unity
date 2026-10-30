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
        public Economy Simulation { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
        public string Feedback { get; private set; } = "";
        private double pendingSeconds;
        private Font font;
        private Text balances, netRates, milestones, capacity, feedbackText, clock;
        private readonly Text[] allocations = new Text[3], rates = new Text[3], upgradeLabels = new Text[3], upgradeEffects = new Text[3];
        private readonly Button[] plus = new Button[3], minus = new Button[3], buy = new Button[3];
        private Button pauseButton, expandCapacity;
        private UnityEngine.UI.Button conversionModeButton;
        private UnityEngine.UI.Text conversionModeDetails;
        private readonly Color ink = new Color(0.88f, 0.93f, 0.98f);
        private readonly Color muted = new Color(0.65f, 0.75f, 0.84f);
        private readonly Color accent = new Color(0.35f, 0.86f, 0.73f);
        private readonly Color warning = new Color(1f, 0.72f, 0.35f);

        private void Awake()
        {
            Simulation = new Economy(enableAlternateConversion);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            Refresh();
        }

        private void Update()
        {
            if (!Paused)
            {
                pendingSeconds += Time.unscaledDeltaTime * Speed;
                // Keep fractional elapsed time and any backlog; never simulate device-clock/offline time.
                int ticks = (int)Math.Min(500, Math.Floor((pendingSeconds + 1e-9) * Economy.TicksPerSecond));
                if (ticks > 0)
                {
                    try
                    {
                        Simulation.AdvanceTicks(ticks);
                        pendingSeconds -= ticks / (double)Economy.TicksPerSecond;
                    }
                    catch (OverflowException)
                    {
                        Paused = true;
                        Feedback = "Simulation paused: numeric storage limit reached.";
                    }
                }
            }
            Refresh();
        }

        public void ChangeAllocation(int index, int delta)
        {
            if (index < 0 || index > 2) return;
            Feedback = Simulation.TrySetAllocation(index, Simulation.Allocation(index) + delta, out string reason)
                ? "Allocation updated." : reason;
            Refresh();
        }

        public void PurchaseUpgrade(int index)
        {
            Feedback = Simulation.TryPurchase(index, out string reason)
                ? "Purchased " + PresentationLabels.Upgrades[index] + "."
                : reason;
            Refresh();
        }

        public void TogglePause() { Paused = !Paused; Refresh(); }
        public void SetSpeed(int speed) { if (speed == 1 || speed == 5 || speed == 20) Speed = speed; Refresh(); }

        public void PurchaseCapacity()
        {
            Feedback = Simulation.TryExpandCapacity(out string reason) ? "+1 capacity available." : reason;
            Refresh();
        }

        public void ToggleConversionMode()
        {
            var next = Simulation.SelectedConversionMode == ConversionMode.Standard ? ConversionMode.Fast : ConversionMode.Standard;
            Feedback = Simulation.TrySetConversionMode(next, out string reason)
                ? "Conversion mode changed. Switching is free." : reason;
            Refresh();
        }

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
            netRates.text = "NET /s     " + A($"A {SignedRate(flow.AProduced - flow.AConsumed)}") + "     " + B($"B {SignedRate(flow.BProduced - flow.BConsumed)}") + "     " + Credit($"Credits {SignedRate(flow.CreditsProduced)}") + "     " + Xp($"XP {SignedRate(flow.XpProduced)}");
            capacity.text = $"CAPACITY   {used} / {Simulation.Capacity}   |   {Simulation.Capacity - used} free";
            milestones.text = Xp("XP GOALS") + "     " + Goal(100) + "     " + Goal(500) + "     " + Goal(2000);
            bool hasExpansion = Simulation.TryGetExpansionCost(out long expansionCost);
            RefreshPrice(expandCapacity, hasExpansion, expansionCost, "+1  |  ", "Max capacity");
            clock.text = $"Simulation {Simulation.Ticks / 10m:0.0}s   |   {Speed}x   |   {(Paused ? "Paused" : "Running")}";
            for (int i = 0; i < 3; i++)
            {
                allocations[i].text = Simulation.Allocation(i).ToString();
                plus[i].interactable = used < Simulation.Capacity;
                minus[i].interactable = Simulation.Allocation(i) > 0;
                bool hasCost = Simulation.TryGetCost(i, out long cost);
                RefreshPrice(buy[i], hasCost, cost);
                upgradeLabels[i].text = $"Efficiency\nLevel {Simulation.Level(i)}";
                string effect = PresentationLabels.Effects[i];
                if (i == Economy.Converter && Simulation.SelectedConversionMode == ConversionMode.Fast)
                    effect = PresentationLabels.FastConversionUpgradeEffect;
                if (i == 0) effect = A(effect);
                else if (i == 1) effect = B(effect);
                else effect = Credit(effect);
                upgradeEffects[i].text = effect;
            }
            if (conversionModeButton != null)
            {
                bool fast = Simulation.SelectedConversionMode == ConversionMode.Fast;
                conversionModeButton.GetComponentInChildren<Text>().text = "Mode: " + (fast ? PresentationLabels.FastConversion : PresentationLabels.StandardConversion);
                decimal output = (fast ? 1.5m : 1m) * (1m + Simulation.Level(Economy.Converter) / 2m);
                conversionModeDetails.text = "Per capacity /s: " + A(fast ? "4 A" : "2 A") + " -> " + B(output.ToString("0.##", CultureInfo.InvariantCulture) + " B") + "  |  Free switch";
            }
            rates[0].text = A($"A {PerSecond(flow.AProduced)} / {PerSecond(flow.MaxAProduced)}");
            rates[1].text = B($"B {PerSecond(flow.BProduced)} / {PerSecond(flow.MaxBProduced)}") + "\n" + A($"A input {PerSecond(flow.AConsumed)}/s") + State(1, flow.ConversionStarved, "A");
            rates[2].text = Credit("Credits") + " + " + Xp("XP") + $" {PerSecond(flow.CreditsProduced)} / {PerSecond(flow.MaxCreditsProduced)}\n" + B($"B input {PerSecond(flow.BConsumed)}/s") + State(2, flow.DevelopmentStarved, "B");
            pauseButton.GetComponentInChildren<Text>().text = Paused ? "Resume" : "Pause";
            feedbackText.text = Feedback;
        }

        private string Goal(int value) => (Simulation.LifetimeXp >= value * Economy.Scale ? "[REACHED] " : "[  ] ") + value.ToString("N0", CultureInfo.InvariantCulture);
        private string State(int index, bool starved, string input) => Simulation.Allocation(index) == 0 ? "   |   Unassigned" : starved ? "   |   ! INPUT STARVED: " + (input == "A" ? A(input) : B(input)) : "";

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
            milestones = Label("Milestones", content.transform, "", 19); Height(milestones.gameObject, 32);
            var capacityRow = Row("CapacityRow", content.transform, 44);
            capacity = Label("Capacity", capacityRow.transform, "", 22); capacity.gameObject.AddComponent<LayoutElement>().preferredWidth = 360;
            expandCapacity = Button("ExpandCapacity", capacityRow.transform, "", PurchaseCapacity, 250);
            StylePriceButton(expandCapacity);
            var heading = Label("ThroughputLegend", content.transform, "OUTPUT /s     Actual / max", 19); heading.color = muted; Height(heading.gameObject, 30);
            var costLegend = Label("CostLegend", content.transform, "All purchases use Credits. Efficiency costs double per level.", 19); costLegend.color = muted; Height(costLegend.gameObject, 26);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                bool showMode = i == Economy.Converter && Simulation.AlternateConversionEnabled;
                var card = Node("Process" + i, content.transform, typeof(Image), typeof(VerticalLayoutGroup)); Height(card, showMode ? 204 : 154);
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
                    conversionModeButton = Button("ConversionMode", modeRow.transform, "", ToggleConversionMode, 220);
                    conversionModeDetails = Label("ConversionModeDetails", modeRow.transform, "", 19);
                    Flexible(conversionModeDetails);
                }
                var upgradeRow = Row("Upgrade" + i, card.transform, 64);
                upgradeLabels[i] = Label("Description", upgradeRow.transform, "", 20); upgradeLabels[i].color = muted; upgradeLabels[i].gameObject.AddComponent<LayoutElement>().preferredWidth = 220;
                buy[i] = Button("Purchase" + i, upgradeRow.transform, "", () => PurchaseUpgrade(index), 250);
                StylePriceButton(buy[i]);
                upgradeEffects[i] = Label("UpgradeEffect" + i, upgradeRow.transform, "", 21); Flexible(upgradeEffects[i], 500);
            }
            feedbackText = Label("Feedback", content.transform, "", 20); feedbackText.color = warning; Height(feedbackText.gameObject, 36);
            var controls = Row("TestControls", content.transform, 44);
            pauseButton = Button("Pause", controls.transform, "Pause", TogglePause);
            Button("Speed1", controls.transform, "1x", () => SetSpeed(1), 70);
            Button("Speed5", controls.transform, "5x", () => SetSpeed(5), 70);
            Button("Speed20", controls.transform, "20x", () => SetSpeed(20), 70);
            clock = Label("Clock", controls.transform, "", 19); Flexible(clock);
            var note = Label("LocalOnly", content.transform, "Session only. Not saved.", 18); note.color = muted; Height(note.gameObject, 30);
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = Node("EventSystem", transform, typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            eventSystem.firstSelectedGameObject = minus[0].gameObject;
            eventSystem.SetSelectedGameObject(minus[0].gameObject);
        }
    }
}
