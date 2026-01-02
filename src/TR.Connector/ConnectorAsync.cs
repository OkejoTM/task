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

            var body = new { login = _login, password = _password };
            var tokenResponse = await _apiClient.PostAsync<TokenResponse>(
                Constants.ApiLogin, 
                body, 
                cancellationToken);

            if (tokenResponse?.data?.access_token == null)
                throw new InvalidOperationException("Failed to obtain access token");

            _apiClient.SetBearerToken(tokenResponse.data.access_token);
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

            var itRoleResponse = await _apiClient.GetAsync<RoleResponse>(
                Constants.ApiRolesAll, 
                cancellationToken);
            
            var itRolePermissions = itRoleResponse.data.Select(role =>
                new Permission(
                    $"{Constants.PermissionTypeItRole},{role.id}",
                    role.name,
                    role.corporatePhoneNumber));

            var rightResponse = await _apiClient.GetAsync<RoleResponse>(
                Constants.ApiRightsAll, 
                cancellationToken);
            
            var rightPermissions = rightResponse.data.Select(right =>
                new Permission(
                    $"{Constants.PermissionTypeRequestRight},{right.id}",
                    right.name,
                    right.corporatePhoneNumber));

            return itRolePermissions.Concat(rightPermissions);
        }

        public async Task<IEnumerable<string>> GetUserPermissionsAsync(string userLogin, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            var itRoleResponse = await _apiClient.GetAsync<UserRoleResponse>(
                string.Format(Constants.ApiUserRoles, userLogin),
                cancellationToken);
            
            var roles = itRoleResponse.data.Select(role => 
                $"{Constants.PermissionTypeItRole},{role.id}");

            var rightResponse = await _apiClient.GetAsync<UserRoleResponse>(
                string.Format(Constants.ApiUserRights, userLogin),
                cancellationToken);
            
            var rights = rightResponse.data.Select(right => 
                $"{Constants.PermissionTypeRequestRight},{right.id}");

            return roles.Concat(rights).ToList();
        }

        public async Task AddUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            if (rightIds == null || !rightIds.Any())
                return;

            if (!await CanModifyUserAsync(userLogin, cancellationToken))
                return;

            foreach (var rightId in rightIds)
            {
                await ModifyUserPermissionAsync(userLogin, rightId, isAdding: true, cancellationToken);
            }
        }

        public async Task RemoveUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            if (rightIds == null || !rightIds.Any())
                return;

            if (!await CanModifyUserAsync(userLogin, cancellationToken))
                return;

            foreach (var rightId in rightIds)
            {
                await ModifyUserPermissionAsync(userLogin, rightId, isAdding: false, cancellationToken);
            }
        }

        private async Task<bool> CanModifyUserAsync(string userLogin, CancellationToken cancellationToken)
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

        private async Task ModifyUserPermissionAsync(string userLogin, string rightId, bool isAdding, CancellationToken cancellationToken)
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

            return Task.FromResult<IEnumerable<Property>>(properties);
        }

        public async Task<IEnumerable<UserProperty>> GetUserPropertiesAsync(string userLogin, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(
                string.Format(Constants.ApiUserById, userLogin),
                cancellationToken);

            if (userResponse?.data == null)
                throw new InvalidOperationException($"Пользователь {userLogin} не найден");

            if (userResponse.data.status == Constants.UserStatusLock)
                throw new InvalidOperationException($"Невозможно получить свойства, пользователь {userLogin} заблокирован");

            var user = userResponse.data;
            return new List<UserProperty>
            {
                new UserProperty(Constants.PropertyLastName, user.lastName ?? string.Empty),
                new UserProperty(Constants.PropertyFirstName, user.firstName ?? string.Empty),
                new UserProperty(Constants.PropertyMiddleName, user.middleName ?? string.Empty),
                new UserProperty(Constants.PropertyTelephoneNumber, user.telephoneNumber ?? string.Empty),
                new UserProperty(Constants.PropertyIsLead, user.isLead.ToString())
            };
        }

        public async Task UpdateUserPropertiesAsync(IEnumerable<UserProperty> properties, string userLogin, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateUserLogin(userLogin);

            if (properties == null || !properties.Any())
                return;

            var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(
                string.Format(Constants.ApiUserById, userLogin),
                cancellationToken);

            if (userResponse?.data == null)
                throw new InvalidOperationException($"Пользователь {userLogin} не найден");

            if (userResponse.data.status == Constants.UserStatusLock)
                throw new InvalidOperationException($"Невозможно обновить свойства, пользователь {userLogin} заблокирован");

            var user = userResponse.data;

            foreach (var property in properties)
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case var name when name.Equals(Constants.PropertyLastName, StringComparison.OrdinalIgnoreCase):
                        user.lastName = property.Value;
                        break;
                    case var name when name.Equals(Constants.PropertyFirstName, StringComparison.OrdinalIgnoreCase):
                        user.firstName = property.Value;
                        break;
                    case var name when name.Equals(Constants.PropertyMiddleName, StringComparison.OrdinalIgnoreCase):
                        user.middleName = property.Value;
                        break;
                    case var name when name.Equals(Constants.PropertyTelephoneNumber, StringComparison.OrdinalIgnoreCase):
                        user.telephoneNumber = property.Value;
                        break;
                    case var name when name.Equals(Constants.PropertyIsLead, StringComparison.OrdinalIgnoreCase):
                        if (bool.TryParse(property.Value, out bool isLeadValue))
                            user.isLead = isLeadValue;
                        break;
                }
            }

            await _apiClient.PutAsync(Constants.ApiUsersEdit, user, cancellationToken);
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

                return userResponse?.data != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task CreateUserAsync(UserToCreate user, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var newUser = new CreateUserDTO
            {
                login = user.Login,
                password = user.HashPassword,
                lastName = GetPropertyValue(user.Properties, Constants.PropertyLastName),
                firstName = GetPropertyValue(user.Properties, Constants.PropertyFirstName),
                middleName = GetPropertyValue(user.Properties, Constants.PropertyMiddleName),
                telephoneNumber = GetPropertyValue(user.Properties, Constants.PropertyTelephoneNumber),
                isLead = bool.TryParse(GetPropertyValue(user.Properties, Constants.PropertyIsLead), out bool isLeadValue) && isLeadValue,
                status = string.Empty
            };

            await _apiClient.PostAsync(Constants.ApiUsersCreate, newUser, cancellationToken);
        }

        private static string GetPropertyValue(IEnumerable<UserProperty> properties, string propertyName)
        {
            return properties?.FirstOrDefault(p => 
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
