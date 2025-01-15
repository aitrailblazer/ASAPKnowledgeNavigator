
namespace Cosmos.Copilot.Models
{
    public class CohereResponse
    {
        public string GeneratedCompletion { get; set; }
        public List<Citation> Citations { get; set; }
        public string FinishReason { get; set; }
        public Usage Usage { get; set; }
    }

    public class Citation
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Text { get; set; }
        public List<Source> Sources { get; set; }
    }

    public class Source
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public Document Document { get; set; }
    }

    public class Document
    {
        public string Id { get; set; }
        public string Snippet { get; set; }
        public string Title { get; set; }
    }

    public class Usage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
