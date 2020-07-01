$cred = gcloud auth print-access-token
$headers = @{ Authorization = "Bearer $cred" }
$PROJECT_ID = "iothealthcare-269209"
$LOCATION = "europe-west4"
$DATASET_ID = "IoTHealthcareDataset"
$FHIR_STORE_ID = "ObservationsDataStore"
#$PATIENT_ID = "9f6a2621-0f66-464f-bd9b-f6a54f2222de" #Ripley
#$PATIENT_ID = "d0be3465-6956-46b0-96fb-9f844a093902" #Weyland
$PATIENT_ID = "1c8fa469-2b2b-4da2-9205-51177c127244" #Hicks
$ENCOUNTER_ID = "5accad37-c56d-469d-9629-23e9a552c9a1"
$DATETIMENOW = Get-Date -Format o
$BPM_VALUE = Get-Random -Minimum 50 -Maximum 220

$observation = '{
  "resourceType": "Observation",
  "status": "final",
  "subject": {
    "reference": "Patient/' + $PATIENT_ID + '"
  },
  "effectiveDateTime": "' + $DATETIMENOW + '",
  "code": {
    "coding": [
      {
        "system": "http://loinc.org",
        "code": "8867-4",
        "display": "Heart rate"
      }
    ]
  },
  "valueQuantity": {
    "value": ' + $BPM_VALUE + ',
    "unit": "bpm"
  },
  "context": {
    "reference": "Encounter/' + $ENCOUNTER_ID + '"
  }
}'

Invoke-RestMethod `
  -Method Post `
  -Headers $headers `
  -ContentType: "application/fhir+json; charset=utf-8" `
  -Body $observation `
  -Uri "https://healthcare.googleapis.com/v1beta1/projects/$PROJECT_ID/locations/$LOCATION/datasets/$DATASET_ID/fhirStores/$FHIR_STORE_ID/fhir/Observation" | ConvertTo-Json