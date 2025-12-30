namespace TR.Connector.Models.Responses
{
    internal class UserResponseData
    {
        public string login { get; set; }
        public string status { get; set; }
    }

    internal class UserResponse
    {
        public List<UserResponseData> data { get; set; }
        public bool success { get; set; }
        public object errorText { get; set; }
        public int count { get; set; }
    }
}
