using Npgsql;

public class LlmResponseRepository : LlmResponseRecordDataAccess, IRepository
{
    private IRepository _repository => this;

    public LlmResponseRecord AddLlmResponseRecord(LlmResponseRecord llmResponseRecord, string sessionId)
    {
        var sqlParam = new NpgsqlParameter[] {
            new("llm_response_id", llmResponseRecord.LlmResponseId),
            new("llm_response_number", llmResponseRecord.LlmResponseNumber),
            new("response_heading", llmResponseRecord.LlmResponseHeading),
            new("response_explanation", llmResponseRecord.LlmResponseExplanation),
            new("session_id", sessionId)
        };

        var result = _repository.ExecuteReader<LlmResponseRecord>(
            "INSERT INTO llm_response (llm_response_id, llm_response_number, response_heading, response_explanation, session_id) " +
            "VALUES (@llm_response_id, @llm_response_number, @response_heading, @response_explanation, @session_id) " +
            "RETURNING llm_response_id",
            sqlParam
        ).Single();

        return result;
    }

    public LlmResponseRecord GetLlmResponseRecord(string sessionId, int LlmResponseNumber)
    {
        var sqlParam = new NpgsqlParameter[] {
            new("session_id", sessionId),
            new("llm_response_number", LlmResponseNumber)
        };

        var result = _repository.ExecuteReader<LlmResponseRecord>(
            "SELECT * FROM llm_response WHERE session_id = @session_id AND llm_response_number = @llm_response_number",
            sqlParam
        ).Single();

        return result;
    }
}