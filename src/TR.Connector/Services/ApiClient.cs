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
            ValidateEndpoint(endpoint);
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            return await ProcessResponseAsync<T>(response, endpoint, cancellationToken);
        }

        public async Task<T> PostAsync<T>(string endpoint, object body, CancellationToken cancellationToken = default) 
            where T : BaseApiResponse
        {
            ValidateEndpoint(endpoint);
            var jsonContent = CreateJsonContent(body);
            var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);
            return await ProcessResponseAsync<T>(response, endpoint, cancellationToken);
        }

        public async Task PostAsync(string endpoint, object body, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(endpoint);
            var jsonContent = CreateJsonContent(body);
            var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);
            await ProcessResponseAsync(response, endpoint, cancellationToken);
        }

        public async Task PutAsync(string endpoint, object? body = null, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(endpoint);
            var jsonContent = body != null ? CreateJsonContent(body) : null;
            var response = await _httpClient.PutAsync(endpoint, jsonContent, cancellationToken);
            await ProcessResponseAsync(response, endpoint, cancellationToken);
        }

        public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            ValidateEndpoint(endpoint);
            var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
            await ProcessResponseAsync(response, endpoint, cancellationToken);
        }

        private static void ValidateEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
        }

        private static StringContent CreateJsonContent(object body)
        {
            return new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        private async Task<T> ProcessResponseAsync<T>(
            HttpResponseMessage response, 
            string endpoint, 
            CancellationToken cancellationToken) 
            where T : BaseApiResponse
        {
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<T>(content) 
                ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");

            ValidateApiResponse(result, endpoint);
            return result;
        }

        private async Task ProcessResponseAsync(
            HttpResponseMessage response, 
            string endpoint, 
            CancellationToken cancellationToken)
        {
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
