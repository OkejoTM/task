namespace TR.Connector.Models.Responses
{
    internal class BaseApiResponse
    {
        public bool success { get; set; }
        public string? errorText { get; set; }
        public int count { get; set; }
    }

    internal class BaseApiResponse<T> : BaseApiResponse
    {
        public T? data { get; set; }
    }
}
