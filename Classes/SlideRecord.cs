public class SlideRecord
{
    public string SlideId { get; set; }
    public int SlideNumber { get; set; }

    public SlideRecord(string slideId, int slideNumber)
    {
        SlideId = slideId;
        SlideNumber = slideNumber;
    }
}