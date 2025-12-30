namespace TR.Connector.Models.Responses
{
    internal class TokenResponseData
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
    }

    internal class TokenResponse
    {
        public TokenResponseData data { get; set; }
        public bool success { get; set; }
        public object errorText { get; set; }
        public object count { get; set; }
    }
}
