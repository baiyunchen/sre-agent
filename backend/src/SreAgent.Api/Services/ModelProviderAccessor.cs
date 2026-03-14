using SreAgent.Framework.Providers;

namespace SreAgent.Api.Services;

public class ModelProviderAccessor : IModelProviderAccessor
{
    private ModelProvider _current;
    private readonly ReaderWriterLockSlim _lock = new();

    public ModelProviderAccessor(ModelProviderOptions initialOptions)
    {
        _current = new ModelProvider(initialOptions);
    }

    public ModelProvider Current
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _current;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void Update(ModelProviderOptions options)
    {
        var newProvider = new ModelProvider(options);
        _lock.EnterWriteLock();
        try
        {
            _current = newProvider;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
