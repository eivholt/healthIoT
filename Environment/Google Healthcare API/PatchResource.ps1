$cred = gcloud auth print-access-token
$headers = @{ Authorization = "Bearer $cred" }
$PROJECT_ID = "iothealthcare-269209"
$LOCATION = "europe-west4"
$DATASET_ID = "IoTHealthcareDataset"
$FHIR_STORE_ID = "LocationsDataStore"
$RESOURCE_ID = "dfdbe4f6-a94c-420e-9e42-da5f260a31b7"

$patch = '[
  {
    "op": "replace",
    "path": "/operationalStatus",
    "value": 
    {
        "code":  "U",
        "display":  "Unoccupied",
        "system":  "http://hl7.org/fhir/v2/0116",
        "version":  "2.8.2"
    }
  }
]'

Invoke-RestMethod `
  -Method Patch `
  -Headers $headers `
  -ContentType: "application/json-patch+json" `
  -Body $patch `
  -Uri "https://healthcare.googleapis.com/v1beta1/projects/$PROJECT_ID/locations/$LOCATION/datasets/$DATASET_ID/fhirStores/$FHIR_STORE_ID/fhir/Location/$RESOURCE_ID" | ConvertTo-Json