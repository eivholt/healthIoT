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

        [FunctionName("CreateObservation")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext executionContext)
        {
            log.LogInformation("C# HTTP trigger function CreateObservation processed a request.");

            using (var cloudHealthcareService = CreateCloudHealthcareService(executionContext))
            {
                try
                {
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    var requestJson = JsonConvert.DeserializeObject<JObject>(requestBody) as dynamic;

                    var cloudPropertiesDictionary = ExtractCloudProperties(requestJson);
                    LogProperties(log, cloudPropertiesDictionary);
                    var telemetryDictionary = ExtractTelemetry(requestJson);
                    LogProperties(log, telemetryDictionary);

                    // The name of the resource to create.
                    string parent = $"projects/{ProjectId}/locations/{Location}/datasets/{DatasetId}/fhirStores/{FhirStoreId}";
                    var createBody = CreateObservationHttpBody(requestJson, telemetryDictionary, cloudPropertiesDictionary["patientId"], cloudPropertiesDictionary["encounterId"]);

                    var createRequest =
                        cloudHealthcareService.Projects.Locations.Datasets.FhirStores.Fhir.Create(createBody, parent, "Observation");

                    JObject response = createRequest.SendAsJObjectAsync(createBody).Result;

                    return new OkObjectResult(response);
                }
                catch (Exception ex)
                {
                    return new BadRequestObjectResult(JsonConvert.SerializeObject(ex));
                }
            }
        }

        private static HttpBody CreateObservationHttpBody(dynamic requestJson, Dictionary<string, object> telemetryDictionary, string patientId, string encounterId)
        {
            Data.HttpBody createBody = new Data.HttpBody();

            var observationJson = new JObject();
            dynamic observation = observationJson;
            observation.resourceType = "Observation";
            observation.status = "final";
            dynamic observationSubject = new JObject();
            observationSubject.reference = $"Patient/{patientId}";
            observation.subject = observationSubject;
            observation.effectiveDateTime = requestJson.timestamp;
            dynamic observationCode = new JObject();
            observationCode.coding = new JArray() as dynamic;
            dynamic observationCoding = MapCapabilityToObservationCoding();
            observationCode.coding.Add(observationCoding);
            observation.code = observationCode;
            dynamic observationValueQuantity = new JObject();
            observationValueQuantity.value = telemetryDictionary["hr"];
            observationValueQuantity.unit = "bpm";
            observation.valueQuantity = observationValueQuantity;
            dynamic observationContext = new JObject();
            observationContext.reference = $"Encounter/{encounterId}";
            observation.context = observationContext;

            createBody.Data = observationJson.ToString();
            return createBody;
        }

        private static dynamic MapCapabilityToObservationCoding()
        {
            dynamic observationCoding = new JObject();
            observationCoding.system = "http://loinc.org";
            observationCoding.code = "8867-4";
            observationCoding.display = "Heart rate";
            return observationCoding;
        }

        private static Dictionary<string, object> ExtractCloudProperties(JObject requestJson)
        {
            var cloudPropertiesDictionary = new Dictionary<string, object>();

            foreach (JProperty property in requestJson.SelectToken("device.cloudProperties"))
            {
                cloudPropertiesDictionary.Add(property.Name, ((JValue)property.Value["value"]).Value);
            }

            return cloudPropertiesDictionary;
        }

        private static Dictionary<string, object> ExtractTelemetry(JObject requestJson)
        {
            var telemetryDictionary = new Dictionary<string, object>();

            foreach (JProperty capability in requestJson.SelectToken("device.telemetry.*"))
            {
                telemetryDictionary.Add(capability.Name, ((JValue)capability.Value["value"]).Value);
            }

            return telemetryDictionary;
        }

        private static void LogProperties(ILogger log, Dictionary<string, object> dictionary)
        {
            foreach (var property in dictionary)
            {
                log.LogInformation($"Found {property.Key}: {property.Value} ({property.Value.GetType().Name}).");
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
        public static async Task<JObject> SendAsJObjectAsync(this ClientServiceRequest<HttpBody> request, HttpBody requestBody)
        {
            var httpRequestMessage = request.CreateRequest();
            httpRequestMessage.Content = new StringContent(requestBody.Data);
            httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/fhir+json");
            var httpResponseMessage = await request.Service.HttpClient.PostAsync(httpRequestMessage.RequestUri.AbsoluteUri, httpRequestMessage.Content);
            httpResponseMessage.EnsureSuccessStatusCode();
            var responseText = await httpResponseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(responseText);
        }
    }
}
