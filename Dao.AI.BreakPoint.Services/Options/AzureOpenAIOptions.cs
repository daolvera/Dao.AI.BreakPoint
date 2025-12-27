namespace Dao.AI.BreakPoint.Services.Options;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure OpenAI API key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The deployment name for the chat completion model
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4";

    /// <summary>
    /// Maximum tokens for the response
    /// </summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>
    /// Temperature for response generation (0-1)
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}
