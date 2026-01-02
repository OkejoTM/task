namespace TR.Connector.Models.Responses
{
    internal class TokenResponseData
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
    }

    internal class TokenResponse : BaseApiResponse<TokenResponseData>
    {
    }
}
