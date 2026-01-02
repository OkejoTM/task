using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TR.Connector.Exceptions;
using TR.Connector.Models.Responses;

namespace TR.Connector.Services
{
    internal class ApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public ApiClient(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        public void SetBearerToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or empty", nameof(token));

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default) 
            where T : BaseApiResponse
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<T>(content) 
                ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");

            ValidateApiResponse(result, endpoint);
            return result;
        }

        public async Task<T> PostAsync<T>(string endpoint, object body, CancellationToken cancellationToken = default) 
            where T : BaseApiResponse
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<T>(content)
                ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");

            ValidateApiResponse(result, endpoint);
            return result;
        }

        public async Task PostAsync(string endpoint, object body, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<BaseApiResponse>(content);
            
            if (result != null)
                ValidateApiResponse(result, endpoint);
        }

        public async Task PutAsync(string endpoint, object? body = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            StringContent? jsonContent = null;
            if (body != null)
            {
                jsonContent = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");
            }

            var response = await _httpClient.PutAsync(endpoint, jsonContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<BaseApiResponse>(content);
            
            if (result != null)
                ValidateApiResponse(result, endpoint);
        }

        public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<BaseApiResponse>(content);
            
            if (result != null)
                ValidateApiResponse(result, endpoint);
        }

        private static void ValidateApiResponse(BaseApiResponse response, string endpoint)
        {
            if (!response.success)
            {
                throw new ApiException(response.errorText, endpoint);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
