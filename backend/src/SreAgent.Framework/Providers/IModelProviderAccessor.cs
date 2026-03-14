namespace SreAgent.Framework.Providers;

/// <summary>
/// Provides access to the current ModelProvider and allows runtime updates.
/// Replaces direct ModelProvider injection to enable hot-swapping providers.
/// </summary>
public interface IModelProviderAccessor
{
    ModelProvider Current { get; }
    void Update(ModelProviderOptions options);
}
