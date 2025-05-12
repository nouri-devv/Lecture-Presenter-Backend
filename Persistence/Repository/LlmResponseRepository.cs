using Npgsql;

public class LlmResponseRepository : LlmResponseDataAccess, IRepository
{
    private IRepository _repository => this;

    public LlmResponse AddLlmResponse(LlmResponse llmResponse)
    {
        var sqlParam = new NpgsqlParameter[] {
            new("session_id", llmResponse.SessionId),
            new("llm_response_number", llmResponse.LlmResponseNumber),
            new("llm_response_heading", llmResponse.LlmResponseHeading),
            new("llm_response_explanation", llmResponse.LlmResponseExplanation)
        };

        var result = _repository.ExecuteReader<LlmResponse>(
            "INSERT INTO llm_responses (session_id, llm_response_number, llm_response_heading, llm_response_explanation) " +
            "VALUES (@session_id, @llm_response_number, @llm_response_heading, @llm_response_explanation) " +
            "RETURNING session_id, llm_response_number, llm_response_heading, llm_response_explanation",
            sqlParam
        ).SingleOrDefault();

        return result;
    }

    public LlmResponse GetLlmResponse(string sessionId, int LlmResponseNumber)
    {
        var sqlParam = new NpgsqlParameter[] {
            new("session_id", sessionId),
            new("llm_response_number", LlmResponseNumber)
        };

        var result = _repository.ExecuteReader<LlmResponse>(
            "SELECT * FROM llm_responses WHERE session_id = @session_id AND llm_response_number = @llm_response_number",
            sqlParam
        ).SingleOrDefault();

        return result;
    }
}