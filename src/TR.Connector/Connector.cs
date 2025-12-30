using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;
using TR.Connector.Models.Responses;
using TR.Connector.Models.DTOs;
using TR.Connector.Services;

namespace TR.Connector
{
    public class Connector : IConnector
    {
        public ILogger Logger { get; set; }

        private string url = "";
        private string login = "";
        private string password = "";
        private string token = "";

        private ApiClient _apiClient;

        //Пустой конструктор
        public Connector() {}

        public void StartUp(string connectionString)
        {
            StartUpAsync(connectionString).GetAwaiter().GetResult();
        }

        private async Task StartUpAsync(string connectionString)
        {
            //Парсим строку подключения.
            Logger.Debug("Строка подключения: " + connectionString);
            foreach (var item in connectionString.Split(';'))
            {
                if (item.StartsWith(Constants.ConnectionStringKeyUrl)) 
                    url = item.Split('=')[1];
                if (item.StartsWith(Constants.ConnectionStringKeyLogin)) 
                    login = item.Split('=')[1];
                if (item.StartsWith(Constants.ConnectionStringKeyPassword)) 
                    password = item.Split('=')[1];
            }

            _apiClient = new ApiClient(url);

            var body = new { login, password };
            var tokenResponse = await _apiClient.PostAsync<TokenResponse>(Constants.ApiLogin, body);
            token = tokenResponse.data.access_token;
            
            _apiClient.SetBearerToken(token);
        }

        public IEnumerable<Permission> GetAllPermissions()
        {
            return GetAllPermissionsAsync().GetAwaiter().GetResult();
        }

        private async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
        {
            var itRoleResponse = await _apiClient.GetAsync<RoleResponse>(Constants.ApiRolesAll);
            var itRolePermissions = itRoleResponse.data.Select(_ => 
                new Permission($"{Constants.PermissionTypeItRole},{_.id}", _.name, _.corporatePhoneNumber));

            var rightResponse = await _apiClient.GetAsync<RoleResponse>(Constants.ApiRightsAll);
            var rightPermissions = rightResponse.data.Select(_ =>
                new Permission($"{Constants.PermissionTypeRequestRight},{_.id}", _.name, _.corporatePhoneNumber));

            return itRolePermissions.Concat(rightPermissions);
        }

        public IEnumerable<string> GetUserPermissions(string userLogin)
        {
            return GetUserPermissionsAsync(userLogin).GetAwaiter().GetResult();
        }

        private async Task<IEnumerable<string>> GetUserPermissionsAsync(string userLogin)
        {
            var itRoleResponse = await _apiClient.GetAsync<UserRoleResponse>(string.Format(Constants.ApiUserRoles, userLogin));
            var result1 = itRoleResponse.data.Select(_ => $"{Constants.PermissionTypeItRole},{_.id}").ToList();

            var rightResponse = await _apiClient.GetAsync<UserRoleResponse>(string.Format(Constants.ApiUserRights, userLogin));
            var result2 = rightResponse.data.Select(_ => $"{Constants.PermissionTypeRequestRight},{_.id}").ToList();

            return result1.Concat(result2).ToList();
        }

        public void AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            AddUserPermissionsAsync(userLogin, rightIds).GetAwaiter().GetResult();
        }

        private async Task AddUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds)
        {
            var userResponse = await _apiClient.GetAsync<UserResponse>(Constants.ApiUsersAll);
            var user = userResponse.data.FirstOrDefault(_ => _.login == userLogin);

            if (user != null && user.status == Constants.UserStatusLock)
            {
                Logger.Error($"Пользователь {userLogin} залочен.");
                return;
            }
            else if (user != null && user.status == Constants.UserStatusUnlock)
            {
                foreach (var rightId in rightIds)
                {
                    var rightStr = rightId.Split(',');
                    switch (rightStr[0])
                    {
                        case Constants.PermissionTypeItRole:
                            await _apiClient.PutAsync(string.Format(Constants.ApiUserAddRole, userLogin, rightStr[1]));
                            break;
                        case Constants.PermissionTypeRequestRight:
                            await _apiClient.PutAsync(string.Format(Constants.ApiUserAddRight, userLogin, rightStr[1]));
                            break;
                        default: 
                            throw new Exception($"Тип доступа {rightStr[0]} не определен");
                    }
                }
            }
        }

        public void RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            RemoveUserPermissionsAsync(userLogin, rightIds).GetAwaiter().GetResult();
        }

        private async Task RemoveUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds)
        {
            var userResponse = await _apiClient.GetAsync<UserResponse>(Constants.ApiUsersAll);
            var user = userResponse.data.FirstOrDefault(_ => _.login == userLogin);

            if (user != null && user.status == Constants.UserStatusLock)
            {
                Logger.Error($"Пользователь {userLogin} залочен.");
                return;
            }
            else if (user != null && user.status == Constants.UserStatusUnlock)
            {
                foreach (var rightId in rightIds)
                {
                    var rightStr = rightId.Split(',');
                    switch (rightStr[0])
                    {
                        case Constants.PermissionTypeItRole:
                            await _apiClient.DeleteAsync(string.Format(Constants.ApiUserDropRole, userLogin, rightStr[1]));
                            break;
                        case Constants.PermissionTypeRequestRight:
                            await _apiClient.DeleteAsync(string.Format(Constants.ApiUserDropRight, userLogin, rightStr[1]));
                            break;
                        default:
                            throw new Exception($"Тип доступа {rightStr[0]} не определен");
                    }
                }
            }
        }

        public IEnumerable<Property> GetAllProperties()
        {
            var props = new List<Property>();
            foreach (var propertyInfo in new UserPropertyData().GetType().GetProperties())
            {
                if(propertyInfo.Name == Constants.PropertyLogin) continue;

                props.Add(new Property(propertyInfo.Name, propertyInfo.Name));
            }
            return props;
        }

        public IEnumerable<UserProperty> GetUserProperties(string userLogin)
        {
            return GetUserPropertiesAsync(userLogin).GetAwaiter().GetResult();
        }

        private async Task<IEnumerable<UserProperty>> GetUserPropertiesAsync(string userLogin)
        {
            var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(string.Format(Constants.ApiUserById, userLogin));

            var user = userResponse.data ?? throw new NullReferenceException($"Пользователь {userLogin} не найден");

            if (user.status == Constants.UserStatusLock)
                throw new Exception($"Невозможно получить свойства, пользователь {userLogin} залочен");

            return user.GetType().GetProperties()
                .Select(_ => new UserProperty(_.Name, _.GetValue(user) as string));
        }

        public void UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
        {
            UpdateUserPropertiesAsync(properties, userLogin).GetAwaiter().GetResult();
        }

        private async Task UpdateUserPropertiesAsync(IEnumerable<UserProperty> properties, string userLogin)
        {
            var userResponse = await _apiClient.GetAsync<UserPropertyResponse>(string.Format(Constants.ApiUserById, userLogin));

            var user = userResponse.data ?? throw new NullReferenceException($"Пользователь {userLogin} не найден");
            if (user.status == Constants.UserStatusLock)
                throw new Exception($"Невозможно обновить свойства, пользователь {userLogin} залочен");

            foreach (var property in properties)
            {
                foreach (var userProp in user.GetType().GetProperties())
                {
                    if (property.Name == userProp.Name)
                    {
                        userProp.SetValue(user, property.Value);
                    }
                }
            }

            await _apiClient.PutAsync(Constants.ApiUsersEdit, user);
        }

        public bool IsUserExists(string userLogin)
        {
            return IsUserExistsAsync(userLogin).GetAwaiter().GetResult();
        }

        private async Task<bool> IsUserExistsAsync(string userLogin)
        {
            var userResponse = await _apiClient.GetAsync<UserResponse>(Constants.ApiUsersAll);
            var user = userResponse.data.FirstOrDefault(_ => _.login == userLogin);

            return user != null;
        }

        public void CreateUser(UserToCreate user)
        {
            CreateUserAsync(user).GetAwaiter().GetResult();
        }

        private async Task CreateUserAsync(UserToCreate user)
        {
            var newUser = new CreateUserDTO()
            {
                login = user.Login,
                password = user.HashPassword,

                lastName = user.Properties.FirstOrDefault(p => p.Name.Equals(Constants.PropertyLastName, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                firstName = user.Properties.FirstOrDefault(p => p.Name.Equals(Constants.PropertyFirstName, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                middleName = user.Properties.FirstOrDefault(p => p.Name.Equals(Constants.PropertyMiddleName, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,

                telephoneNumber = user.Properties.FirstOrDefault(p => p.Name.Equals(Constants.PropertyTelephoneNumber, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                isLead = bool.TryParse(user.Properties.FirstOrDefault(p => p.Name.Equals(Constants.PropertyIsLead, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty, out bool isLeadValue)
                    ? isLeadValue
                    : false,

                status = string.Empty
            };

            await _apiClient.PostAsync(Constants.ApiUsersCreate, newUser);
        }
    }
}
