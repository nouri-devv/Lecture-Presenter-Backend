public interface LlmResponseDataAccess
{
    LlmResponse AddLlmResponse(LlmResponse llmResponse);
    LlmResponse GetLlmResponse(string sessionId, int llmReponseNumber);
}