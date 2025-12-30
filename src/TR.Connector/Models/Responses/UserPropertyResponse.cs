using TR.Connector.Models.DTOs;

namespace TR.Connector.Models.Responses
{
    internal class UserPropertyResponse
    {
        public UserPropertyData data { get; set; }
        public bool success { get; set; }
        public object errorText { get; set; }
        public int count { get; set; }
    }
}
