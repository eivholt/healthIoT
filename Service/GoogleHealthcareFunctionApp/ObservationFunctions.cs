using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Google.Apis.CloudHealthcare.v1beta1;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Data = Google.Apis.CloudHealthcare.v1beta1.Data;
using Newtonsoft.Json.Linq;
using Google.Apis.Requests;
using Google.Apis.CloudHealthcare.v1beta1.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace GoogleHealthcareFunctionApp
{
    public static class ObservationFunctions
    {
        private const string ProjectId = "iothealthcare-269209";
        private const string Location = "europe-west4";
        private const string DatasetId = "IoTHealthcareDataset";
        private const string FhirStoreId = "ObservationsDataStore";

        [FunctionName("PatchObservation")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext executionContext)
        {
            log.LogInformation("C# HTTP trigger function PatchObservation processed a request.");

            using (var cloudHealthcareService = CreateCloudHealthcareService(executionContext))
            {
                try
                {
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    var requestJson = JsonConvert.DeserializeObject<JObject>(requestBody);

                    var telemetryDictionary = new Dictionary<string, object>();
                    ExtractTelemetry(requestJson, telemetryDictionary);
                    LogTelemetry(log, telemetryDictionary);

                    // Observation id to patch
                    var resourceId = "58bfeb6f-f3bd-424c-a6f9-2815af956ae9";
                    // The name of the resource to patch.
                    string resourceName = $"projects/{ProjectId}/locations/{Location}/datasets/{DatasetId}/fhirStores/{FhirStoreId}/fhir/Observation/{resourceId}";

                    Data.HttpBody patchBody = new Data.HttpBody();

                    var jsonPatch = new JArray();
                    var patchValue = new JObject();
                    patchValue.Add("op", "replace");
                    patchValue.Add("path", "/valueQuantity/value");
                    patchValue.Add("value", hrValue.Value);
                    jsonPatch.Add(patchValue);

                    patchBody.Data = jsonPatch.ToString();

                    var patchRequest =
                        cloudHealthcareService.Projects.Locations.Datasets.FhirStores.Fhir.Patch(patchBody, resourceName);

                    JObject response = patchRequest.PatchAsJObjectAsync(patchBody).Result;

                    return new OkObjectResult(response);
                }
                catch (Exception ex)
                {
                    return new BadRequestObjectResult(ex);
                }
            }
        }

        private static void ExtractTelemetry(JObject requestJson, Dictionary<string, object> telemetryDictionary)
        {
            foreach (JProperty capability in requestJson.SelectToken("device.telemetry.*"))
            {
                telemetryDictionary.Add(capability.Name, ((JValue)capability.Value["value"]).Value);
            }
        }

        private static void LogTelemetry(ILogger log, Dictionary<string, object> telemetryDictionary)
        {
            foreach (var capability in telemetryDictionary)
            {
                log.LogInformation($"Found {capability.Key}: {capability.Value} ({capability.Value.GetType().Name}).");
            }
        }

        private static CloudHealthcareService CreateCloudHealthcareService(ExecutionContext executionContext)
        {
            return new CloudHealthcareService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GetCredential(executionContext),
                ApplicationName = "GoogleHealthcareFunctionApp/0.1"
            });
        }

        public static GoogleCredential GetCredential(ExecutionContext executionContext)
        {
            //files\\
            var credential = Task.Run(() => GoogleCredential.FromFile($"{ Directory.GetParent(executionContext.FunctionDirectory).FullName}\\iothealthcare-269209-44479b79a3cf.json")).Result;
            if (credential.IsCreateScopedRequired)
            {
                credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform"); //https://www.googleapis.com/auth/cloud-healthcare
            }
            return credential;
        }
    }

    public static class CloudHealthcareServiceExtensions
    {
        public static async Task<JObject> PatchAsJObjectAsync(this ClientServiceRequest<HttpBody> request, HttpBody requestBody)
        {
            var httpRequestMessage = request.CreateRequest();
            httpRequestMessage.Content = new StringContent(requestBody.Data);
            httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json-patch+json");
            var httpResponseMessage = await request.Service.HttpClient.PatchAsync(httpRequestMessage.RequestUri.AbsoluteUri, httpRequestMessage.Content);
            httpResponseMessage.EnsureSuccessStatusCode();
            var responseText = await httpResponseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(responseText);
        }
    }
}
