namespace NoRM
{
    using System.Collections.Generic;

    internal static class ConnectionProviderFactory
    {
        private static readonly IDictionary<string, IConnectionProvider> _providers = new Dictionary<string, IConnectionProvider>();
        private static readonly IDictionary<string, ConnectionStringBuilder> _cachedBuilders = new Dictionary<string, ConnectionStringBuilder>();

        public static IConnectionProvider Create(string connectionString)
        {
            ConnectionStringBuilder builder;
            if (!_cachedBuilders.TryGetValue(connectionString, out builder))
            {
                builder = ConnectionStringBuilder.Create(connectionString);
                _cachedBuilders.Add(connectionString, builder);
            }
            
            IConnectionProvider provider;            
            if (!_providers.TryGetValue(connectionString, out provider))
            {
                //todo: consider thread safety
                provider = CreateNewProvider(builder);
                _providers[connectionString] = provider;
            }
            return provider;
        }

        private static IConnectionProvider CreateNewProvider(ConnectionStringBuilder builder)
        {
            if (builder.Pooled)
            {
                return new PooledConnectionProvider(builder);
            }
            return new NormalConnectionProvider(builder);
        }
    }
}