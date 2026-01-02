using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;

namespace TR.Connector
{
    public class Connector : IConnector, IDisposable
    {
        private readonly ConnectorAsync _asyncConnector;
        private bool _disposed;

        public ILogger Logger
        {
            get => _asyncConnector.Logger;
            set => _asyncConnector.Logger = value;
        }

        public Connector()
        {
            _asyncConnector = new ConnectorAsync();
        }

        public void StartUp(string connectionString)
        {
            _asyncConnector.StartUpAsync(connectionString)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public IEnumerable<Permission> GetAllPermissions()
        {
            return _asyncConnector.GetAllPermissionsAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public IEnumerable<string> GetUserPermissions(string userLogin)
        {
            return _asyncConnector.GetUserPermissionsAsync(userLogin)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public void AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            _asyncConnector.AddUserPermissionsAsync(userLogin, rightIds)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public void RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            _asyncConnector.RemoveUserPermissionsAsync(userLogin, rightIds)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return _asyncConnector.GetAllPropertiesAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public IEnumerable<UserProperty> GetUserProperties(string userLogin)
        {
            return _asyncConnector.GetUserPropertiesAsync(userLogin)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public void UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
        {
            _asyncConnector.UpdateUserPropertiesAsync(properties, userLogin)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public bool IsUserExists(string userLogin)
        {
            return _asyncConnector.IsUserExistsAsync(userLogin)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public void CreateUser(UserToCreate user)
        {
            _asyncConnector.CreateUserAsync(user)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _asyncConnector?.Dispose();
                _disposed = true;
            }
        }
    }
}