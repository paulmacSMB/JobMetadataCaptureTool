namespace JobMetadataCaptureTool
{
    public class ExtractCompanyNameFromTitle
    {
        public ExtractCompanyNameFromTitle()
        {
           
        }

        public static string ExtractNameFromTitle(string companyName)
        {
            if (string.IsNullOrEmpty(companyName)) return "";

            // Examples:
            // "Careers at Google | Google Jobs" => "Google"
            // "Amazon.jobs: Help us build Earth’s most customer-centric company." => "Amazon"
            var separators = new[] { "|", "-", ":" };

            foreach (var sep in separators)
            {
                var parts = companyName.Split(sep);
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.ToLower().Contains("career") || trimmed.ToLower().Contains("job"))
                        continue;

                    // Capitalize first letters, remove obvious junk
                    if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length < 50)
                        return trimmed;
                }
            }

            return companyName; // fallback
        }
    }
}
