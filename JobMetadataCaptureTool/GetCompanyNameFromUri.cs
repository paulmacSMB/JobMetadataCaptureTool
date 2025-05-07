

using System.Globalization;

namespace JobMetadataCaptureTool
{
    public class GetCompanyNameFromUri
    {
        public GetCompanyNameFromUri()
        {
           
        }

        public static string ExtractNameFromTitle(Uri uri)
        {
            var ignoredSubdomains = new[] { "www", "jobs", "careers", "workday", "boards", "apply", "employment" };

            var hostParts = uri.Host.Split('.');
            var filteredParts = hostParts
                .Where(part => !ignoredSubdomains.Contains(part.ToLower()))
                .ToArray();

            string companyCandidate = (filteredParts.Length >= 2)
                ? filteredParts[filteredParts.Length - 2]
                : filteredParts.FirstOrDefault() ?? "Unknown";

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(companyCandidate);
        }
    }
}
