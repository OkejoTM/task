using TR.Connectors.Api.Entities;

namespace TR.Connector.Interfaces
{
    public interface IConnectorAsync
    {
        Task StartUpAsync(string connectionString, CancellationToken cancellationToken = default);
        
        Task CreateUserAsync(UserToCreate user, CancellationToken cancellationToken = default);
        
        Task<IEnumerable<Property>> GetAllPropertiesAsync(CancellationToken cancellationToken = default);
        
        Task<IEnumerable<UserProperty>> GetUserPropertiesAsync(string userLogin, CancellationToken cancellationToken = default);
        
        Task<bool> IsUserExistsAsync(string userLogin, CancellationToken cancellationToken = default);
        
        Task UpdateUserPropertiesAsync(IEnumerable<UserProperty> properties, string userLogin, CancellationToken cancellationToken = default);
        
        Task<IEnumerable<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);
        
        Task AddUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds, CancellationToken cancellationToken = default);
        
        Task RemoveUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds, CancellationToken cancellationToken = default);
        
        Task<IEnumerable<string>> GetUserPermissionsAsync(string userLogin, CancellationToken cancellationToken = default);
    }
}
