using Npgsql;

public class LlmResponseRepository : LlmResponseRecordDataAccess, IRepository
{
    private IRepository _repository => this;

    public LlmResponseRecord AddLlmResponseRecord(LlmResponseRecord llmResponseRecord)
    {
        var sqlParam = new NpgsqlParameter[] {
            new("llm_response_id", llmResponseRecord.LlmResponseId),
            new("session_id", llmResponseRecord.SessionId),
            new("llm_response_number", llmResponseRecord.LlmResponseNumber),
            new("response_heading", llmResponseRecord.LlmResponseHeading),
            new("response_explanation", llmResponseRecord.LlmResponseExplanation)
        };

        var result = _repository.ExecuteReader<LlmResponseRecord>(
            "INSERT INTO llm_response (llm_response_id, session_id, llm_response_number, response_heading, response_explanation) " +
            "VALUES (@llm_response_id, @session_id, @llm_response_number, @response_heading, @response_explanation) " +
            "RETURNING llm_response_id, session_id, llm_response_number, response_heading, response_explanation",
            sqlParam
        ).SingleOrDefault();

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
        ).SingleOrDefault();

        return result;
    }
}