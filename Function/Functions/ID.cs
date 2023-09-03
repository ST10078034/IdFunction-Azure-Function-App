using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace IdFunction.Functions
{
    public static class IdFunction
    {
        // VaccinationInfo object to store and return object to user
        // better formating of the data for json
        private class VaccinationInfo
        {
            public string Identifier { get; set; }
            public bool Vaccinated { get; set; }
            public string VaccinationDate { get; set; }
            public string Name { get; set; }
            public string Clinic { get; set; }
        }

        // ValidationResult object used to return whether or not input is valid
        // if invalid an error message is shown to the user explaining the error
        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        // This Azure Function handles requests to retrieve vaccination information based on an identifier
        [FunctionName("IdFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "id/{id?}")] HttpRequest req,
            string id,
            ILogger log)
        {
            try
            {
                // return error to user if api endpoint is not entered
                if (string.IsNullOrEmpty(id))
                {
                    log.LogInformation("Vaccination function processed a request with error.");
                    return new BadRequestObjectResult(new
                    {
                        Error = "Invalid input",
                        Message = "Please enter numerical 13 digit ID or 9 character passport number starting with a letter [A-Z].",
                    });
                }

                // ternary expression determining whether the input is a id or passport
                // if condidtion is true then id is assigned to responseMessage if flase passport is assigned to responseMessage
                string responseMessage = id.Length == 13
                ? $"Received ID: {id}"
                : $"Received Passport: {id}";

                // used to check if input is vaild
                ValidationResult validationResult = ValidateInput(id);

                // return error to user if input above is invalid
                if (!validationResult.IsValid)
                {
                    log.LogInformation("Vaccination function processed a request with error.");
                    return new BadRequestObjectResult(new
                    {
                        Error = "Invalid input",
                        Message = validationResult.ErrorMessage
                    });
                }

                log.LogInformation("Vaccination function processed a request successfully.");

                // check if input is in the json using async
                VaccinationInfo vaccinationInfo = await GetVaccinationInfo(id);

                // return the vaule from the switch in a list as json, if switch does not contain value then create a empty list that is returned to user
                object responseObject = new
                {
                    Message = responseMessage,
                    VaccinationData = vaccinationInfo,
                };

                // Serialise the json response object and Format it wit indentation 
                string json = JsonConvert.SerializeObject(responseObject, Formatting.Indented);

                // return the json response to endpoint
                return new ContentResult
                {
                    Content = json,
                    ContentType = "application/json",
                    StatusCode = 200
                };
            }
            // catch any errors that may be due to user error or server-side errors
            catch (Exception ex)
            {
                log.LogError($"An error occurred: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

        }

        // method used to check if input has valid syntax
        // if syntax is invalid return error to user
        private static ValidationResult ValidateInput(string id)
        {
            // check if user input is a vallid id (13 numbers)
            // or valid passport (First character is a letter and last 8 characters are numerical)
            if (id.Length == 13 || id.Length == 9 && Regex.IsMatch(id, "^[A-Za-z][0-9]{8}$"))
            {
                return new ValidationResult { IsValid = true };
            }
            else
            {
                // return ValidationResult object that has error message to user explaining error
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Please enter numerical 13 digit ID or 9 character passport number starting with a letter [A-Z]."
                };
            }
        }

        // method used to check if input from user has values attached to it
        // if value not found in the switch it returns (vaccinated = false) and all other values are null
        private static async Task<VaccinationInfo> GetVaccinationInfo(string identifier)
        {
            // json file location in the Hosted azure function 
            // Used Kudu Debugger to find the file location of the json when I deployed the function
            string jsonFilePath = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "site", "Data", "wwwroot", "vaccinations.json");

            // Json File path if you want to run locally
            // uncomment line bellow and comment out line above
            //  |
            //  v
            // string jsonFilePath = "Data/vaccinations.json";

            string json = jsonFilePath;

            // Read the JSON file asynchronously
            string jsonContent = await File.ReadAllTextAsync(json);

            // Deserialize the JSON content
            List<VaccinationInfo> vaccinationData = JsonConvert.DeserializeObject<List<VaccinationInfo>>(jsonContent);

            // Search for the relevant identifier in the deserialized data using LINQ
            VaccinationInfo matchedInfo = vaccinationData.FirstOrDefault(info => info.Identifier == identifier);

            // null-coalescing operator used to return matchedInfo if it is found in the json file
            // if matchedInfo is null then a VaccinationInfo object that has Vaccinated variable set to false is returned
            return matchedInfo ?? new VaccinationInfo { Identifier = identifier, Vaccinated = false };

        }
    }
}
