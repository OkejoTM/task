namespace TR.Connector.Models.Responses
{
    internal class RoleResponseData
    {
        public int id { get; set; }
        public string name { get; set; }
        public string corporatePhoneNumber { get; set; }
    }

    internal class RoleResponse
    {
        public List<RoleResponseData> data { get; set; }
        public bool success { get; set; }
        public object errorText { get; set; }
        public int count { get; set; }
    }
}
