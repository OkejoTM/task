namespace TR.Connector.Exceptions
{
    internal class ApiException : Exception
    {
        public string? ErrorText { get; }
        public string? Endpoint { get; }

        public ApiException(string? errorText, string? endpoint = null) 
            : base(BuildMessage(errorText, endpoint))
        {
            ErrorText = errorText;
            Endpoint = endpoint;
        }

        private static string BuildMessage(string? errorText, string? endpoint)
        {
            var message = $"API request failed: {errorText ?? "Unknown error"}";
            if (!string.IsNullOrWhiteSpace(endpoint))
                message += $" (endpoint: {endpoint})";
            return message;
        }
    }
}
