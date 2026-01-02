namespace TR.Connector.Models.Responses
{
    internal class UserResponseData
    {
        public string login { get; set; }
        public string status { get; set; }
    }

    internal class UserResponse : BaseApiResponse<List<UserResponseData>>
    {
    }
}
