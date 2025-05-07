public class LlmResponseRecord
{
    public string ResponseId { get; set; }
    public string ResponseHeading { get; set; } // <- typo fixed
    public string ResponseExplanation { get; set; }

    public LlmResponseRecord(string responseId, string responseHeading, string responseExplanation)
    {
        ResponseId = responseId;
        ResponseHeading = responseHeading;
        ResponseExplanation = responseExplanation;
    }
}
