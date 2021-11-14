using Microsoft.Azure.Cosmos.Table;

namespace AzureBlobExplorer.Models
{
    [System.Flags]
    public enum AccessType
    {
        NoAccess = 0, Write = 1, ReadWrite = 2, Delete = 4
    }

    public class SASViewModel : TableEntity
    {        
        public string URL { get; set; }
        public string GroupId { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public int Access { get; set; }
        public AccessType AccessPermission
        {
            get
            {
                return (AccessType)this.Access;
            }
        }
    }
}