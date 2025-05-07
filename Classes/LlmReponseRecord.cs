public class LlmResponseRecord
{
    public string ResponseId { get; set; }
    public string ReppnseHeading { get; set; }
    public string ResponseExplanation { get; set; }

    public LlmResponseRecord(string responseId, string reppnseHeading, string responseExplanation)
    {
        ResponseId = responseId;
        ReppnseHeading = reppnseHeading;
        ResponseExplanation = responseExplanation;
    }
}