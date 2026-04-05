using CrmEarlyBound;
using D365_Shared_Library.Repository;
using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Collections.Generic;
using System.Linq;

namespace Kiddopay.BLL.Services
{
    public class InventoryService(IBaseRepository<blser_Ingredient> ingredient) : IinventoryService
    {
        readonly IBaseRepository<blser_Ingredient> _category = ingredient;

        public List<InventoryDTO> GetAll()
        {
            var query = $@"
                    <fetch version=""1.0"" mapping=""logical"" savedqueryid=""f1015f4f-d0af-40f3-b34f-68333a4ddbee"" no-lock=""false"" distinct=""true"">
                    <entity name=""blser_ingredient"">
                    <attribute name=""statecode""/>
                    <attribute name=""blser_ingredientid""/>
                    <attribute name=""blser_name""/>
                    <attribute name=""createdon""/>
                    <order attribute=""blser_name"" descending=""false""/>
                    <filter type=""and"">
                    <condition attribute=""statecode"" operator=""eq"" value=""0""/>
                    <condition attribute=""blser_name"" operator=""not-null""/>
                    </filter>
                    </entity>
                    </fetch>
                ";

            // Execute FetchXML
            var rows = _category.GetAll(query);

            // Map to DTO
            return rows!
                .Select(m => new InventoryDTO
                {
                    Name = m.blser_Name,
                })
                .ToList();
        }
    }
}
