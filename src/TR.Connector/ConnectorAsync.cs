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

        private string _url = string.Empty;
        private string _login = string.Empty;
        private string _password = string.Empty;
        private ApiClient _apiClient;
        private bool _disposed;

        public ConnectorAsync()
        {
        }

        public async Task StartUpAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            Logger?.Debug("Строка подключения: " + connectionString);

            ParseConnectionString(connectionString);

            _apiClient = new ApiClient(_url);

            try
            {
                var body = new { login = _login, password = _password };
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

        private void ParseConnectionString(string connectionString)
        {
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
                        _url = value;
                        break;
                    case Constants.ConnectionStringKeyLogin:
                        _login = value;
                        break;
                    case Constants.ConnectionStringKeyPassword:
                        _password = value;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(_url))
                throw new InvalidOperationException("URL not found in connection string");
            if (string.IsNullOrWhiteSpace(_login))
                throw new InvalidOperationException("Login not found in connection string");
            if (string.IsNullOrWhiteSpace(_password))
                throw new InvalidOperationException("Password not found in connection string");
        }

        public async Task<IEnumerable<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            try
            {
                var itRoleResponse = await _apiClient.GetAsync<RoleResponse>(
                    Constants.ApiRolesAll,
                    cancellationToken);

                var itRolePermissions = (itRoleResponse.data ?? new List<RoleResponseData>())
                    .Select(role => new Permission(
                        $"{Constants.PermissionTypeItRole},{role.id}",
                        role.name,
                        role.corporatePhoneNumber));

                var rightResponse = await _apiClient.GetAsync<RoleResponse>(
                    Constants.ApiRightsAll,
                    cancellationToken);

                var rightPermissions = (rightResponse.data ?? new List<RoleResponseData>())
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

                var roles = (itRoleResponse.data ?? new List<RoleResponseData>())
                    .Select(role => $"{Constants.PermissionTypeItRole},{role.id}");

                var rightResponse = await _apiClient.GetAsync<UserRoleResponse>(
                    string.Format(Constants.ApiUserRights, userLogin),
                    cancellationToken);

                var rights = (rightResponse.data ?? new List<RoleResponseData>())
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

            if (rightIds == null || !rightIds.Any())
            {
                Logger?.Warn($"No permissions to remove for user {userLogin}");
                return;
            }

            try
            {
                if (!await CanModifyUserAsync(userLogin, cancellationToken))
                    return;

                var permissionsList = rightIds.ToList();
                Logger?.Debug($"Removing {permissionsList.Count} permissions from user {userLogin}");

                foreach (var rightId in permissionsList)
                {
                    await ModifyUserPermissionAsync(userLogin, rightId, isAdding: false, cancellationToken);
                }

                Logger?.Debug($"Successfully removed {permissionsList.Count} permissions from user {userLogin}");
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to remove permissions for user {userLogin}: {ex.ErrorText}");
                throw;
            }
        }

        private async Task<bool> CanModifyUserAsync(string userLogin, CancellationToken cancellationToken)
        {
            try
            {
                var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(
                    string.Format(Constants.ApiUserById, userLogin),
                    cancellationToken);

                if (userResponse?.data == null)
                {
                    Logger?.Error($"Пользователь {userLogin} не найден.");
                    return false;
                }

                if (userResponse.data.status == Constants.UserStatusLock)
                {
                    Logger?.Error($"Пользователь {userLogin} заблокирован.");
                    return false;
                }

                return true;
            }
            catch (ApiException ex)
            {
                Logger?.Error($"Failed to check user status for {userLogin}: {ex.ErrorText}");
                return false;
            }
        }

        private async Task ModifyUserPermissionAsync(string userLogin, string rightId, bool isAdding,
            CancellationToken cancellationToken)
        {
            var parts = rightId.Split(',');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid permission format: {rightId}", nameof(rightId));

            var permissionType = parts[0];
            var permissionId = parts[1];

            var endpoint = permissionType switch
            {
                Constants.PermissionTypeItRole => isAdding
                    ? string.Format(Constants.ApiUserAddRole, userLogin, permissionId)
                    : string.Format(Constants.ApiUserDropRole, userLogin, permissionId),
                Constants.PermissionTypeRequestRight => isAdding
                    ? string.Format(Constants.ApiUserAddRight, userLogin, permissionId)
                    : string.Format(Constants.ApiUserDropRight, userLogin, permissionId),
                _ => throw new ArgumentException($"Неизвестный тип прав: {permissionType}", nameof(rightId))
            };

            if (isAdding)
                await _apiClient.PutAsync(endpoint, cancellationToken: cancellationToken);
            else
                await _apiClient.DeleteAsync(endpoint, cancellationToken);
        }

        public Task<IEnumerable<Property>> GetAllPropertiesAsync(CancellationToken cancellationToken = default)
        {
            var properties = new List<Property>
            {
                new Property(Constants.PropertyLastName, Constants.PropertyLastName),
                new Property(Constants.PropertyFirstName, Constants.PropertyFirstName),
                new Property(Constants.PropertyMiddleName, Constants.PropertyMiddleName),
                new Property(Constants.PropertyTelephoneNumber, Constants.PropertyTelephoneNumber),
                new Property(Constants.PropertyIsLead, Constants.PropertyIsLead)
            };

            Logger?.Debug($"Retrieved {properties.Count} property definitions");
            return Task.FromResult<IEnumerable<Property>>(properties);
        }

        public async Task<IEnumerable<UserProperty>> GetUserPropertiesAsync(string userLogin,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            try
            {
                var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(
                    string.Format(Constants.ApiUserById, userLogin),
                    cancellationToken);

                if (userResponse?.data == null)
                    throw new InvalidOperationException($"Пользователь {userLogin} не найден");

                if (userResponse.data.status == Constants.UserStatusLock)
                    throw new InvalidOperationException(
                        $"Невозможно получить свойства, пользователь {userLogin} заблокирован");

                var user = userResponse.data;
                var properties = new List<UserProperty>
                {
                    new UserProperty(Constants.PropertyLastName, user.lastName ?? string.Empty),
                    new UserProperty(Constants.PropertyFirstName, user.firstName ?? string.Empty),
                    new UserProperty(Constants.PropertyMiddleName, user.middleName ?? string.Empty),
                    new UserProperty(Constants.PropertyTelephoneNumber, user.telephoneNumber ?? string.Empty),
                    new UserProperty(Constants.PropertyIsLead, user.isLead.ToString())
                };

                Logger?.Debug($"Retrieved {properties.Count} properties for user {userLogin}");
                return properties;
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

            if (properties == null || !properties.Any())
            {
                Logger?.Warn($"No properties to update for user {userLogin}");
                return;
            }

            try
            {
                var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(
                    string.Format(Constants.ApiUserById, userLogin),
                    cancellationToken);

                if (userResponse?.data == null)
                    throw new InvalidOperationException($"Пользователь {userLogin} не найден");

                if (userResponse.data.status == Constants.UserStatusLock)
                    throw new InvalidOperationException(
                        $"Невозможно обновить свойства, пользователь {userLogin} заблокирован");

                var user = userResponse.data;
                var propertyList = properties.ToList();

                Logger?.Debug($"Updating {propertyList.Count} properties for user {userLogin}");

                foreach (var property in propertyList)
                {
                    switch (property.Name.ToLowerInvariant())
                    {
                        case var name when name.Equals(Constants.PropertyLastName, StringComparison.OrdinalIgnoreCase):
                            user.lastName = property.Value;
                            break;
                        case var name when name.Equals(Constants.PropertyFirstName, StringComparison.OrdinalIgnoreCase):
                            user.firstName = property.Value;
                            break;
                        case var name
                            when name.Equals(Constants.PropertyMiddleName, StringComparison.OrdinalIgnoreCase):
                            user.middleName = property.Value;
                            break;
                        case var name when name.Equals(Constants.PropertyTelephoneNumber,
                            StringComparison.OrdinalIgnoreCase):
                            user.telephoneNumber = property.Value;
                            break;
                        case var name when name.Equals(Constants.PropertyIsLead, StringComparison.OrdinalIgnoreCase):
                            if (bool.TryParse(property.Value, out bool isLeadValue))
                                user.isLead = isLeadValue;
                            break;
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
                return false;
            }
            catch (Exception ex)
            {
                Logger?.Error($"Error checking if user {userLogin} exists: {ex.Message}");
                return false;
            }
        }

        public async Task CreateUserAsync(UserToCreate user, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            try
            {
                var newUser = new CreateUserDTO
                {
                    login = user.Login,
                    password = user.HashPassword,
                    lastName = GetPropertyValue(user.Properties, Constants.PropertyLastName),
                    firstName = GetPropertyValue(user.Properties, Constants.PropertyFirstName),
                    middleName = GetPropertyValue(user.Properties, Constants.PropertyMiddleName),
                    telephoneNumber = GetPropertyValue(user.Properties, Constants.PropertyTelephoneNumber),
                    isLead = bool.TryParse(GetPropertyValue(user.Properties, Constants.PropertyIsLead),
                        out bool isLeadValue) && isLeadValue,
                    status = string.Empty
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
