namespace TR.Connector.Models.Responses
{
    internal class RoleResponseData
    {
        public int id { get; set; }
        public string name { get; set; }
        public string corporatePhoneNumber { get; set; }
    }

    internal class RoleResponse : BaseApiResponse<List<RoleResponseData>>
    {
    }
}
