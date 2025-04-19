public class LLMRequests
{
    public int Id { get; set; }
    public JsonContent Content { get; set; }
    public string ApiKey { get; set; }
    
    public LLMRequests(int id, JsonContent content, string apiKey)
    {
        Id = id;
        Content = content;
        ApiKey = apiKey;
    }
}