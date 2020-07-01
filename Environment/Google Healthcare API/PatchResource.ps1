$cred = gcloud auth print-access-token
$headers = @{ Authorization = "Bearer $cred" }
$PROJECT_ID = "iothealthcare-269209"
$LOCATION = "europe-west4"
$DATASET_ID = "IoTHealthcareDataset"
$FHIR_STORE_ID = "ObservationsDataStore"
$OBSERVATION_ID = "58bfeb6f-f3bd-424c-a6f9-2815af956ae9" #Ripley BPM
$BPM_VALUE = Get-Random -Minimum 50 -Maximum 220

  $patch = '[
  {
    "op": "replace",
    "path": "/valueQuantity/value",
    "value": ' + $BPM_VALUE + '
  }
]'

Invoke-RestMethod `
  -Method Patch `
  -Headers $headers `
  -ContentType: "application/json-patch+json" `
  -Body $patch `
  -Uri "https://healthcare.googleapis.com/v1beta1/projects/iothealthcare-269209/locations/europe-west4/datasets/IoTHealthcareDataset/fhirStores/ObservationsDataStore/fhir/Observation/58bfeb6f-f3bd-424c-a6f9-2815af956ae9" | ConvertTo-Json