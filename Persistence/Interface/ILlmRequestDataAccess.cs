public interface LlmResponseRecordDataAccess
{
    LlmResponseRecord AddLlmResponseRecord(LlmResponseRecord llmResponseRecord);
    LlmResponseRecord GetLlmResponseRecord(string sessionId, int llmReponseNumber);
}