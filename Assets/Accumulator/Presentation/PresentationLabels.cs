namespace Accumulator.Presentation
{
    // Temporary presentation vocabulary. Economic rules never read these labels.
    public static class PresentationLabels
    {
        public const string ResourceA = "Resource A";
        public const string ResourceB = "Resource B";
        public const string Credits = "Credits";
        public const string LifetimeXp = "Lifetime XP";
        public const string StandardConversion = "Standard";
        public const string FastConversion = "Fast";
        public const string FastConversionUpgradeEffect = "+0.75 B / capacity /s";
        public const string PassiveProduction = "Passive";
        public const string TimingProduction = "Timing";
        public const string TimingProductionUpgradeEffect = "+2 A / capacity / success; +3 A / capacity / critical";
        public const string ResourceAColor = "#79D2FF";
        public const string ResourceBColor = "#FFD279";
        public const string CreditsColor = "#76E5B8";
        public const string XpColor = "#D6B3FF";
        public static readonly string[] Processes = { "Production", "Conversion", "Development" };
        public static readonly string[] Upgrades = { "Production efficiency", "Conversion efficiency", "Development efficiency" };
        public static readonly string[] Effects =
        {
            "+1 A / capacity /s",
            "+0.5 B / capacity /s",
            "+0.5 Credits + XP / capacity /s"
        };
    }
}
