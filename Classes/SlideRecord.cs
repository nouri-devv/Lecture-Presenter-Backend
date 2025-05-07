public class SlideRecord
{
    public string SlideId { get; set; }
    public int SlideNumber { get; set; }
    public string SlideLocation { get; set; }
    public LlmResponseRecord SlideResponse { get; set; } = new LlmResponseRecord("", "", "");
    public TextToSpeechRecord SlideTextToSpeech { get; set; } = new TextToSpeechRecord("", "");

    public SlideRecord(string slideId, int slideNumber, string slideLocation)
    {
        SlideId = slideId;
        SlideNumber = slideNumber;
        SlideLocation = slideLocation;
    }
}