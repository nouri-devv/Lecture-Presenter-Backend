public interface LlmResponseRecordDataAccess
{
    LlmResponseRecord AddLlmResponseRecord(LlmResponseRecord llmResponseRecord, string sessionId);
    LlmResponseRecord GetLlmResponseRecord(string sessionId, int llmReponseNumber);
}