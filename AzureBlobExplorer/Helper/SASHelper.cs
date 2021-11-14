using AzureBlobExplorer.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace AzureBlobExplorer.Helper
{
    public class SASHelper
    {
        private List<SASViewModel> SASViewModels { get; set; }

        public SASHelper(IConfiguration configuration)
        {
            SASViewModels = new List<SASViewModel>();
            SASViewModels = ContainerHelper.QueryTable(configuration["AccountName"], configuration["AccountKey"], configuration["TableName"], configuration["Query"]);
        }

        public List<SASViewModel> GetSASes4User(ClaimsPrincipal user)
        {
            List<SASViewModel> allowedSAS = SASViewModels.Where(x => user.IsInRole(x.GroupId)).ToList();
            return allowedSAS;
        }

        public string GetSASURL(string name)
        {
            return SASViewModels.Where(s => s.Name == name).Select(s => s.URL).FirstOrDefault();
        }

        public int GetAccessType(string name)
        {
            return SASViewModels.Where(s => s.Name == name).Select(s => s.Access).FirstOrDefault();
        }
    }
}