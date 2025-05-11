public interface ISlideDataAccess
{
    SlideRecord CreateSlide(SlideRecord slideRecord, string slideLocation);
    SlideRecord GetSlide(string sessionId, int slideNumber);
}