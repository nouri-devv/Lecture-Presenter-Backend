public class LlmResponseRecord
{
    public string ResponseId { get; set; }
    public int LlmReponseNumber { get; set; }
    public string ResponseHeading { get; set; }
    public string ResponseExplanation { get; set; }

    public LlmResponseRecord(string responseId, int llmReponseNumber, string responseHeading, string responseExplanation)
    {
        ResponseId = responseId;
        LlmReponseNumber = llmReponseNumber;
        ResponseHeading = responseHeading;
        ResponseExplanation = responseExplanation;
    }
}
