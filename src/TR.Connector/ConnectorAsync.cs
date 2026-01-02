using System.Reflection;
using TR.Connector.Configs;
using TR.Connector.Exceptions;
using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;
using TR.Connector.Interfaces;
using TR.Connector.Models.Responses;
using TR.Connector.Models.DTOs;
using TR.Connector.Services;

namespace TR.Connector
{
    public class ConnectorAsync : IConnectorAsync, IDisposable
    {
        public ILogger Logger { get; set; }

        private ConnectionConfig _connectorConfig;
        private ApiClient _apiClient;
        private bool _disposed;

        private static readonly Dictionary<string, Action<UserPropertyData, string>> PropertySetters = 
            new(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.PropertyLastName] = (user, value) => user.lastName = value,
                [Constants.PropertyFirstName] = (user, value) => user.firstName = value,
                [Constants.PropertyMiddleName] = (user, value) => user.middleName = value,
                [Constants.PropertyTelephoneNumber] = (user, value) => user.telephoneNumber = value,
                [Constants.PropertyIsLead] = (user, value) => 
                {
                    if (bool.TryParse(value, out bool isLeadValue))
                        user.isLead = isLeadValue;
                }
            };

        private static readonly PropertyInfo[] AllUserProperties = 
            typeof(UserPropertyData).GetProperties();
        
        private static readonly PropertyInfo[] UserPropertiesWithoutLogin = 
            AllUserProperties.Where(p => p.Name != "login").ToArray();
        
        public ConnectorAsync()
        {
        }

        public async Task StartUpAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            Logger?.Debug("Строка подключения: " + connectionString);

            _connectorConfig = ParseConnectionString(connectionString);

            _apiClient = new ApiClient(_connectorConfig.Url);

            try
            {
                var body = new
                {
                    login = _connectorConfig.Login,
                    password = _connectorConfig.Password
                };

                var tokenResponse = await _apiClient.PostAsync<TokenResponse>(
                    Constants.ApiLogin,
                    body,
                    cancellationToken);

                if (tokenResponse.data?.access_token == null)
                    throw new InvalidOperationException("Failed to obtain access token");

                _apiClient.SetBearerToken(tokenResponse.data.access_token);
                Logger?.Debug("Authentication successful");
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Authentication failed: {ex.ErrorText}");
                throw;
            }
        }

        private static ConnectionConfig ParseConnectionString(string connectionString)
        {
            string? url = null;
            string? login = null;
            string? password = null;

            foreach (var item in connectionString.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                var parts = item.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key.ToLowerInvariant())
                {
                    case Constants.ConnectionStringKeyUrl:
                        url = value;
                        break;
                    case Constants.ConnectionStringKeyLogin:
                        login = value;
                        break;
                    case Constants.ConnectionStringKeyPassword:
                        password = value;
                        break;
                }
            }

            return new ConnectionConfig
            {
                Url = url ?? throw new InvalidOperationException("URL not found in connection string"),
                Login = login ?? throw new InvalidOperationException("Login not found in connection string"),
                Password = password ?? throw new InvalidOperationException("Password not found in connection string")
            };
        }

        public async Task<IEnumerable<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            try
            {
                var itRoleResponse = await _apiClient.GetAsync<RoleResponse>(
                    Constants.ApiRolesAll,
                    cancellationToken);

                var itRolePermissions = (itRoleResponse.data ?? Enumerable.Empty<RoleResponseData>())
                    .Select(role => new Permission(
                        $"{Constants.PermissionTypeItRole},{role.id}",
                        role.name,
                        role.corporatePhoneNumber));

                var rightResponse = await _apiClient.GetAsync<RoleResponse>(
                    Constants.ApiRightsAll,
                    cancellationToken);

                var rightPermissions = (rightResponse.data ?? Enumerable.Empty<RoleResponseData>())
                    .Select(right => new Permission(
                        $"{Constants.PermissionTypeRequestRight},{right.id}",
                        right.name,
                        right.corporatePhoneNumber));

                var result = itRolePermissions.Concat(rightPermissions).ToList();
                Logger?.Debug($"Retrieved {result.Count} permissions");

                return result;
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to get all permissions: {ex.ErrorText}");
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetUserPermissionsAsync(string userLogin,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            try
            {
                var itRoleResponse = await _apiClient.GetAsync<UserRoleResponse>(
                    string.Format(Constants.ApiUserRoles, userLogin),
                    cancellationToken);

                var roles = (itRoleResponse.data ?? Enumerable.Empty<RoleResponseData>())
                    .Select(role => $"{Constants.PermissionTypeItRole},{role.id}");

                var rightResponse = await _apiClient.GetAsync<UserRoleResponse>(
                    string.Format(Constants.ApiUserRights, userLogin),
                    cancellationToken);

                var rights = (rightResponse.data ?? Enumerable.Empty<RoleResponseData>())
                    .Select(right => $"{Constants.PermissionTypeRequestRight},{right.id}");

                var result = roles.Concat(rights).ToList();
                Logger?.Debug($"Retrieved {result.Count} permissions for user {userLogin}");

                return result;
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to get permissions for user {userLogin}: {ex.ErrorText}");
                throw;
            }
        }

        public async Task AddUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            if (rightIds == null || !rightIds.Any())
            {
                Logger?.Warn($"No permissions to add for user {userLogin}");
                return;
            }

            try
            {
                if (!await CanModifyUserAsync(userLogin, cancellationToken))
                    return;

                var permissionsList = rightIds.ToList();
                Logger?.Debug($"Adding {permissionsList.Count} permissions to user {userLogin}");

                foreach (var rightId in permissionsList)
                {
                    await ModifyUserPermissionAsync(userLogin, rightId, isAdding: true, cancellationToken);
                }

                Logger?.Debug($"Successfully added {permissionsList.Count} permissions to user {userLogin}");
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to add permissions for user {userLogin}: {ex.ErrorText}");
                throw;
            }
        }


        public async Task RemoveUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            var rightIdsList = rightIds.ToList();
            if (rightIdsList.Count == 0)
            {
                Logger?.Warn($"No permissions to remove for user {userLogin}");
                return;
            }

            try
            {
                if (!await CanModifyUserAsync(userLogin, cancellationToken))
                    return;

                Logger?.Debug($"Removing {rightIdsList.Count} permissions from user {userLogin}");

                foreach (var rightId in rightIdsList)
                {
                    await ModifyUserPermissionAsync(userLogin, rightId, isAdding: false, cancellationToken);
                }

                Logger?.Debug($"Successfully removed {rightIdsList.Count} permissions from user {userLogin}");
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to remove permissions for user {userLogin}: {ex.ErrorText}");
                throw;
            }
        }

        public Task<IEnumerable<Property>> GetAllPropertiesAsync(CancellationToken cancellationToken = default)
        {
            var props = UserPropertiesWithoutLogin
                .Select(p => new Property(p.Name, p.Name))
                .ToList();

            return Task.FromResult<IEnumerable<Property>>(props);
        }

        public async Task<IEnumerable<UserProperty>> GetUserPropertiesAsync(string userLogin,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            try
            {
                var user = await GetAndValidateUserAsync(userLogin, cancellationToken);

                return AllUserProperties
                    .Select(p => new UserProperty(p.Name, p.GetValue(user)?.ToString() ?? string.Empty));
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to get properties for user {userLogin}: {ex.ErrorText}");
                throw;
            }
        }

        public async Task UpdateUserPropertiesAsync(IEnumerable<UserProperty> properties, string userLogin,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            var userProperties = properties.ToList();
            if (userProperties.Count == 0)
            {
                Logger?.Warn($"No properties to update for user {userLogin}");
                return;
            }

            try
            {
                var user = await GetAndValidateUserAsync(userLogin, cancellationToken);

                Logger?.Debug($"Updating {userProperties.Count} properties for user {userLogin}");

                foreach (var property in userProperties)
                {
                    if (PropertySetters.TryGetValue(property.Name, out var setter))
                    {
                        setter(user, property.Value);
                    }
                    else
                    {
                        Logger?.Warn($"Unknown property: {property.Name}");
                    }
                }

                await _apiClient.PutAsync(Constants.ApiUsersEdit, user, cancellationToken);
                Logger?.Debug($"Successfully updated properties for user {userLogin}");
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to update properties for user {userLogin}: {ex.ErrorText}");
                throw;
            }
        }

        public async Task<bool> IsUserExistsAsync(string userLogin, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            try
            {
                var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(
                    string.Format(Constants.ApiUserById, userLogin),
                    cancellationToken);

                var exists = userResponse?.data != null;
                Logger?.Debug($"User {userLogin} exists: {exists}");
                return exists;
            }
            catch (ApiException ex)
            {
                Logger?.Warn($"User {userLogin} does not exist: {ex.ErrorText}");
            }
            catch (Exception ex)
            {
                Logger?.Error($"Error checking if user {userLogin} exists: {ex.Message}");
            }
            return false;
        }

        public async Task CreateUserAsync(UserToCreate user, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            try
            {
                var newUser = new CreateUserDto
                {
                    login = user.Login,
                    password = user.HashPassword,
                    lastName = GetPropertyValue(user.Properties, Constants.PropertyLastName),
                    firstName = GetPropertyValue(user.Properties, Constants.PropertyFirstName),
                    middleName = GetPropertyValue(user.Properties, Constants.PropertyMiddleName),
                    telephoneNumber = GetPropertyValue(user.Properties, Constants.PropertyTelephoneNumber),
                    isLead = bool.TryParse(GetPropertyValue(user.Properties, Constants.PropertyIsLead),
                        out bool isLeadValue) && isLeadValue,
                    status = Constants.UserStatusUnlock
                };

                Logger?.Debug($"Creating user {user.Login}");
                await _apiClient.PostAsync(Constants.ApiUsersCreate, newUser, cancellationToken);
                Logger?.Debug($"Successfully created user {user.Login}");
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to create user {user.Login}: {ex.ErrorText}");
                throw;
            }
        }

        private async Task<UserPropertyData?> TryGetValidUserAsync(
            string userLogin,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(
                    string.Format(Constants.ApiUserById, userLogin),
                    cancellationToken).ConfigureAwait(false);

                if (userResponse?.data == null)
                {
                    Logger?.Error($"Пользователь {userLogin} не найден");
                    return null;
                }

                if (userResponse.data.status == Constants.UserStatusLock)
                {
                    Logger?.Error($"Пользователь {userLogin} заблокирован");
                    return null;
                }

                return userResponse.data;
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to get user {userLogin}: {ex.ErrorText}");
                return null;
            }
        }
        
        private async Task<UserPropertyData> GetAndValidateUserAsync(
            string userLogin,
            CancellationToken cancellationToken = default)
        {
            var user = await TryGetValidUserAsync(userLogin, cancellationToken);
    
            if (user == null)
                throw new InvalidOperationException($"User {userLogin} us not found or locked");
    
            return user;
        }

        private async Task<bool> CanModifyUserAsync(string userLogin, CancellationToken cancellationToken)
        {
            return await TryGetValidUserAsync(userLogin, cancellationToken) != null;
        }

        private async Task ModifyUserPermissionAsync(string userLogin, string rightId, bool isAdding,
            CancellationToken cancellationToken)
        {
            var parts = rightId.Split(',');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid permission format: {rightId}", nameof(rightId));

            var permissionType = parts[0];
            var permissionId = parts[1];

            var endpoint = PermissionEndpoints.GetEndpoint(permissionType, userLogin, permissionId, isAdding);

            if (isAdding)
                await _apiClient.PutAsync(endpoint, cancellationToken: cancellationToken);
            else
                await _apiClient.DeleteAsync(endpoint, cancellationToken);
        }
        
        private static string GetPropertyValue(IEnumerable<UserProperty> properties, string propertyName)
        {
            return properties.FirstOrDefault(p =>
                p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        }

        private void EnsureInitialized()
        {
            if (_apiClient == null)
                throw new InvalidOperationException("Connector not initialized. Call StartUpAsync first.");
        }

        private static void ValidateUserLogin(string userLogin)
        {
            if (string.IsNullOrWhiteSpace(userLogin))
                throw new ArgumentException("User login cannot be null or empty", nameof(userLogin));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _apiClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
