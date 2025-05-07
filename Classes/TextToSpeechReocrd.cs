public class TextToSpeechRecord
{
    public string TextToSpeechId { get; set; }
    public string TextToSpeechLocation { get; set; }

    public TextToSpeechRecord(string textToSpeechId, string textToSpeechLocation)
    {
        TextToSpeechId = textToSpeechId;
        TextToSpeechLocation = textToSpeechLocation;
    }
}