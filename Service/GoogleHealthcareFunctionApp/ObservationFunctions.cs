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

namespace GoogleHealthcareFunctionApp
{
    public static class ObservationFunctions
    {
        [FunctionName("PatchObservation")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext executionContext)
        {
            log.LogInformation("C# HTTP trigger function PatchObservation processed a request.");

            int? hrValue = null;

            CloudHealthcareService cloudHealthcareService = new CloudHealthcareService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GetCredential(executionContext),
                ApplicationName = "GoogleHealthcareFunctionApp/0.1"
            });

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            JObject requestJson = JObject.Parse(requestBody);
            //hrValue = requestJson.SelectToken("device.telemetry").First.SelectToken("hr.value").Value<int>();
            hrValue = requestJson.SelectToken("device.telemetry.*.hr.value").Value<int>();

            if (!hrValue.HasValue)
            {
                return new NotFoundObjectResult(requestJson);
            }

            var projectId = "iothealthcare-269209";
            var location = "europe-west4";
            var datasetId = "IoTHealthcareDataset";
            var fhirStoreId = "ObservationsDataStore";
            var resourceId = "58bfeb6f-f3bd-424c-a6f9-2815af956ae9";

            // The name of the resource to patch.
            string name = $"projects/{projectId}/locations/{location}/datasets/{datasetId}/fhirStores/{fhirStoreId}/fhir/Observation/{resourceId}";

            Data.HttpBody patchBody = new Data.HttpBody();

            var jsonPatch = new JArray();
            var patchValue = new JObject();
            patchValue.Add("op", "replace");
            patchValue.Add("path", "/valueQuantity/value");
            patchValue.Add("value", hrValue.Value);
            jsonPatch.Add(patchValue);

            patchBody.Data = jsonPatch.ToString();

            var patchRequest =
                cloudHealthcareService.Projects.Locations.Datasets.FhirStores.Fhir.Patch(patchBody, name);

            JObject response = patchRequest.PatchAsJObjectAsync(patchBody).Result;

            return new OkObjectResult(response);
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
