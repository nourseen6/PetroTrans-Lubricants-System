namespace PetroTrans.Application.Assistant;

public interface IAssistantProvider
{
    string Name { get; }
    bool IsRequired { get; }
    string DetectIntent(string text);
}

public sealed class LocalDeterministicAssistantProvider : IAssistantProvider
{
    public string Name => "local-deterministic";
    public bool IsRequired => false;

    public string DetectIntent(string text) => AssistantIntentDetector.Detect(text);
}
