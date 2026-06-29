namespace Localizer_App.Models
{
    internal class ValStats
    {
        // Helper class to hold temporary validation statistics for calculations.
        public double TotalScore { get; set; }
        public int Excellent { get; set; }
        public int Good { get; set; }
        public int NeedsReview { get; set; }
    }
}
