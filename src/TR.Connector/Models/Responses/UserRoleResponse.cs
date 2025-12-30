namespace TR.Connector.Models.Responses
{
    internal class UserRoleResponse
    {
        public List<RoleResponseData> data { get; set; }
        public bool success { get; set; }
        public object errorText { get; set; }
        public int count { get; set; }
    }
}
