$cred = gcloud auth print-access-token
$headers = @{ Authorization = "Bearer $cred" }
$PROJECT_ID = "iothealthcare-269209"
$LOCATION = "europe-west4"
$DATASET_ID = "IoTHealthcareDataset"
$FHIR_STORE_ID = "ObservationsDataStore"
$PATIENT_ID = "9f6a2621-0f66-464f-bd9b-f6a54f2222de"

Invoke-RestMethod `
  -Method Get `
  -Headers $headers `
  -Uri "https://healthcare.googleapis.com/v1beta1/projects/$PROJECT_ID/locations/$LOCATION/datasets/$DATASET_ID/fhirStores/$FHIR_STORE_ID/fhir/Patient/$PATIENT_ID/`$everything" | ConvertTo-Json -Depth 11