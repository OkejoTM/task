namespace TR.Connector
{
    internal static class Constants
    {
        // API Endpoints
        public const string ApiLogin = "api/v1/login";
        public const string ApiRolesAll = "api/v1/roles/all";
        public const string ApiRightsAll = "api/v1/rights/all";
        public const string ApiUsersAll = "api/v1/users/all";
        public const string ApiUsersCreate = "api/v1/users/create";
        public const string ApiUsersEdit = "api/v1/users/edit";
        public const string ApiUserById = "api/v1/users/{0}";
        public const string ApiUserRoles = "api/v1/users/{0}/roles";
        public const string ApiUserRights = "api/v1/users/{0}/rights";
        public const string ApiUserAddRole = "api/v1/users/{0}/add/role/{1}";
        public const string ApiUserAddRight = "api/v1/users/{0}/add/right/{1}";
        public const string ApiUserDropRole = "api/v1/users/{0}/drop/role/{1}";
        public const string ApiUserDropRight = "api/v1/users/{0}/drop/right/{1}";

        // Permission Types
        public const string PermissionTypeItRole = "ItRole";
        public const string PermissionTypeRequestRight = "RequestRight";

        // User Status
        public const string UserStatusLock = "Lock";
        public const string UserStatusUnlock = "Unlock";

        // Connection String Keys
        public const string ConnectionStringKeyUrl = "url";
        public const string ConnectionStringKeyLogin = "login";
        public const string ConnectionStringKeyPassword = "password";

        // User Properties
        public const string PropertyLogin = "login";
        public const string PropertyLastName = "lastName";
        public const string PropertyFirstName = "firstName";
        public const string PropertyMiddleName = "middleName";
        public const string PropertyTelephoneNumber = "telephoneNumber";
        public const string PropertyIsLead = "isLead";
    }
    
    internal static class PermissionEndpoints
    {
        private static readonly Dictionary<(string Type, bool IsAdding), string> EndpointTemplates = new()
        {
            [(Constants.PermissionTypeItRole, true)] = Constants.ApiUserAddRole,
            [(Constants.PermissionTypeItRole, false)] = Constants.ApiUserDropRole,
            [(Constants.PermissionTypeRequestRight, true)] = Constants.ApiUserAddRight,
            [(Constants.PermissionTypeRequestRight, false)] = Constants.ApiUserDropRight
        };

        public static string GetEndpoint(string permissionType, string userLogin, string permissionId, bool isAdding)
        {
            if (!EndpointTemplates.TryGetValue((permissionType, isAdding), out var template))
                throw new ArgumentException($"Неизвестный тип прав: {permissionType}", nameof(permissionType));

            return string.Format(template, userLogin, permissionId);
        }
    }

}
