using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TR.Connector.Services
{
    internal class ApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public ApiClient(string baseUrl)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        public void SetBearerToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<T> GetAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content);
        }

        public async Task<T> PostAsync<T>(string endpoint, object body)
        {
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(body), 
                Encoding.UTF8, 
                "application/json");
            
            var response = await _httpClient.PostAsync(endpoint, jsonContent);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content);
        }

        public async Task PostAsync(string endpoint, object body)
        {
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(body), 
                Encoding.UTF8, 
                "application/json");
            
            var response = await _httpClient.PostAsync(endpoint, jsonContent);
            response.EnsureSuccessStatusCode();
        }

        public async Task PutAsync(string endpoint, object body = null)
        {
            StringContent jsonContent = null;
            if (body != null)
            {
                jsonContent = new StringContent(
                    JsonSerializer.Serialize(body), 
                    Encoding.UTF8, 
                    "application/json");
            }
            
            var response = await _httpClient.PutAsync(endpoint, jsonContent);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteAsync(string endpoint)
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            response.EnsureSuccessStatusCode();
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
