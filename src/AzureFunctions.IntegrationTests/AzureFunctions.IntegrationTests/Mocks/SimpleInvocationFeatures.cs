using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Mocks;

/// <summary>
/// Simple mock implementation of IInvocationFeatures for testing
/// </summary>
public class SimpleInvocationFeatures : IInvocationFeatures
{
    private readonly Dictionary<Type, object> _features = new();

    /// <inheritdoc/>
    public void Set<T>(T instance)
    {
        if (instance != null)
        {
            _features[typeof(T)] = instance;
        }
    }

    /// <inheritdoc/>
    public T? Get<T>()
    {
        if (_features.TryGetValue(typeof(T), out var feature))
        {
            return (T)feature;
        }
        return default;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
    {
        return _features.GetEnumerator();
    }

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
