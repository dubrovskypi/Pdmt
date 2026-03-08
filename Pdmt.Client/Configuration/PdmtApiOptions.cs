namespace Pdmt.Client.Configuration
{
    public class PdmtApiOptions
    {
        public const string SectionName = "PdmtApi";
        public string ClientName { get; set; } = null!;
        public string BaseUrl { get; set; } = null!;
    }
}
