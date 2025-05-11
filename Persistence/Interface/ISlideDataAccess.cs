public interface ISlideDataAccess
{
    SlideRecord CreateSlide(SlideRecord slideRecord);
    SlideRecord GetSlide(string sessionId, int slideNumber);
}