$cred = gcloud auth print-access-token
$headers = @{ Authorization = "Bearer $cred" }
$PROJECT_ID = "iothealthcare-269209"
$LOCATION = "europe-west4"
$DATASET_ID = "IoTHealthcareDataset"
$FHIR_STORE_ID = "ObservationsDataStore"
$PATIENT_ID = "9f6a2621-0f66-464f-bd9b-f6a54f2222de"

$encounter = '{
  "resourceType": "Encounter",
  "status": "in-progress",
  "class": {
    "system": "http://hl7.org/fhir/v3/ActCode",
    "code": "HH",
    "display": "home health"
  },
  "contained": [
    {
      "resourceType": "Location",
      "id": "home",
      "description": "Clients home",
      "mode": "kind"
    }
  ],
  "reason": [
    {
      "text": "The patient had an abnormal heart rate and respiratory rate. She was concerned about this."
    }
  ],
  "subject": {
    "reference": "Patient/' + $PATIENT_ID + '"
  },
  "period": {
    "start": "2020-06-25T16:00:00+02:00"
  }  
}'

Invoke-RestMethod `
  -Method Post `
  -Headers $headers `
  -ContentType: "application/fhir+json; charset=utf-8" `
  -Body $encounter `
  -Uri "https://healthcare.googleapis.com/v1beta1/projects/$PROJECT_ID/locations/$LOCATION/datasets/$DATASET_ID/fhirStores/$FHIR_STORE_ID/fhir/Encounter" | ConvertTo-Json