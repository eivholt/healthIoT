/*
  SPO2 and heart rate sensor with eInk display and LoRaWAN transmission.
  By: Eivind Holt
  Date: 14th July 2020
  https://www.hackster.io/eivholt/global-scale-remote-health-monitor-4d9064
  https://github.com/eivholt/healthIoT/tree/master/Device/GrasshopperMax3010x
  It is best to attach the sensor to your finger using a rubber band or other tightening 
  device. Humans are generally bad at applying constant pressure to a thing. When you 
  press your finger against the sensor it varies enough to cause the blood in your 
  finger to flow differently which causes the sensor readings to go wonky.
*/

#include "secrets.h"
#include <STM32L0.h>
#include <Wire.h>
#include "MAX30105.h"
#include "spo2_algorithm.h"
#include "LoRaWAN.h"
#include "Adafruit_EPD.h"

#define EPD_CS     10
#define EPD_DC      9
#define SRAM_CS     8//11
#define EPD_RESET   5 // can set to -1 and share with microcontroller Reset!
#define EPD_BUSY    4 // can set to -1 to not use a pin (will wait a fixed delay)

/* 1.54" tricolor EPD */
Adafruit_IL0373 display(152, 152, EPD_DC, EPD_RESET, EPD_CS, SRAM_CS, EPD_BUSY);
#define COLOR1 EPD_BLACK
#define COLOR2 EPD_RED

static byte mydata[6];

MAX30105 particleSensor;

float VDDA, VBAT, VUSB, mcuTemp;

#define MAX_BRIGHTNESS 255

#if defined(__AVR_ATmega328P__) || defined(__AVR_ATmega168__)
//Arduino Uno doesn't have enough SRAM to store 100 samples of IR led data and red led data in 32-bit format
//To solve this problem, 16-bit MSB of the sampled data will be truncated. Samples become 16-bit data.
uint16_t irBuffer[100]; //infrared LED sensor data
uint16_t redBuffer[100];  //red LED sensor data
#else
uint32_t irBuffer[100]; //infrared LED sensor data
uint32_t redBuffer[100];  //red LED sensor data
#endif

int32_t bufferLength; //data length
int32_t spo2; //SPO2 value
int8_t validSPO2; //indicator to show if the SPO2 calculation is valid
int32_t heartRate; //heart rate value
int8_t validHeartRate; //indicator to show if the heart rate calculation is valid

byte pulseLED = 13; //Must be on PWM pin

void setup()
{
  Serial.begin(115200); // initialize serial communication at 115200 bits per second:
  while (!Serial) {
    ; // wait for serial port to connect. Needed for native USB port only
  }
  //pinMode(pulseLED, OUTPUT);

  // Initialize sensor
  if (!particleSensor.begin(Wire, I2C_SPEED_FAST)) //Use default I2C port, 400kHz speed
  {
    Serial.println(F("MAX30105 was not found. Please check wiring/power."));
    while (1);
  }

  Serial.println(F("Attach sensor to finger with rubber band. Press any key to start conversion"));
  //while (Serial.available() == 0) ; //wait until user presses a key
  //Serial.read();

  byte ledBrightness = 60; //Options: 0=Off to 255=50mA
  byte sampleAverage = 4; //Options: 1, 2, 4, 8, 16, 32
  byte ledMode = 2; //Options: 1 = Red only, 2 = Red + IR, 3 = Red + IR + Green
  byte sampleRate = 100; //Options: 50, 100, 200, 400, 800, 1000, 1600, 3200
  int pulseWidth = 411; //Options: 69, 118, 215, 411
  int adcRange = 4096; //Options: 2048, 4096, 8192, 16384

  particleSensor.setup(ledBrightness, sampleAverage, ledMode, sampleRate, pulseWidth, adcRange); //Configure sensor with these settings

  /*
  - Asia       AS923
  - Australia  AU915
  - Europe     EU868
  - India      IN865
  - Korea      KR920
  - US         US915 (64 + 8 channels)
  */
  LoRaWAN.begin(EU868);
  LoRaWAN.setADR(true);
  //LoRaWAN.setDataRate(1);
  //LoRaWAN.setTxPower(10);
  LoRaWAN.setSubBand(2); // for TTN 

  LoRaWAN.joinOTAA(appEui, appKey);
}

void loop()
{
  bufferLength = 100; //buffer length of 100 stores 4 seconds of samples running at 25sps

  //read the first 100 samples, and determine the signal range
  for (byte i = 0 ; i < bufferLength ; i++)
  {
    while (particleSensor.available() == false) //do we have new data?
      particleSensor.check(); //Check the sensor for new data

    redBuffer[i] = particleSensor.getRed();
    irBuffer[i] = particleSensor.getIR();
    particleSensor.nextSample(); //We're finished with this sample so move to next sample

    Serial.print(F("red="));
    Serial.print(redBuffer[i], DEC);
    Serial.print(F(", ir="));
    Serial.println(irBuffer[i], DEC);
  }

  //calculate heart rate and SpO2 after first 100 samples (first 4 seconds of samples)
  maxim_heart_rate_and_oxygen_saturation(irBuffer, bufferLength, redBuffer, &spo2, &validSPO2, &heartRate, &validHeartRate);

  //Continuously taking samples from MAX30102.  Heart rate and SpO2 are calculated every 1 second
  while (1)
  {
    //dumping the first 25 sets of samples in the memory and shift the last 75 sets of samples to the top
    for (byte i = 25; i < 100; i++)
    {
      redBuffer[i - 25] = redBuffer[i];
      irBuffer[i - 25] = irBuffer[i];
    }

    //take 25 sets of samples before calculating the heart rate.
    for (byte i = 75; i < 100; i++)
    {
      while (particleSensor.available() == false) //do we have new data?
        particleSensor.check(); //Check the sensor for new data

      redBuffer[i] = particleSensor.getRed();
      irBuffer[i] = particleSensor.getIR();
      particleSensor.nextSample(); //We're finished with this sample so move to next sample

      //send samples and calculation result to terminal program through UART
//      Serial.print(F("red="));
//      Serial.print(redBuffer[i], DEC);
//      Serial.print(F(", ir="));
//      Serial.print(irBuffer[i], DEC);
    }

    Serial.print(F("HR="));
    Serial.print(heartRate, DEC);

    Serial.print(F(", HRvalid="));
    Serial.print(validHeartRate, DEC);

    Serial.print(F(", SPO2="));
    Serial.print(spo2, DEC);

    Serial.print(F(", SPO2Valid="));
    Serial.println(validSPO2, DEC);

    //After gathering 25 new samples recalculate HR and SP02
    maxim_heart_rate_and_oxygen_saturation(irBuffer, bufferLength, redBuffer, &spo2, &validSPO2, &heartRate, &validHeartRate);

    if(validSPO2 && spo2 > 0 && spo2 < 100 && validHeartRate && heartRate > 0 && heartRate < 350)
    {
      if (!LoRaWAN.busy() && LoRaWAN.joined())
      {
          VDDA = STM32L0.getVDDA();
          VBAT = STM32L0.getVBAT();
          VUSB = STM32L0.getVBUS();
          mcuTemp = STM32L0.getTemperature();

          Serial.print(F("VDDA = ")); Serial.println(VDDA, 2);
          Serial.print(F("VBAT = ")); Serial.println(VBAT, 2);
          int voltage = 0;
          if(VBAT > 0)
          {
            voltage = VBAT*1000;
          } else { voltage = VDDA*1000; }
          //if(VUSB ==  1)  Serial.println("USB Connected!"); 
          Serial.print(F("STM32L0 MCU Temperature = ")); Serial.println(mcuTemp);

          int devtemp = (int)(mcuTemp*100);
          
          mydata[0] = constrain(spo2, 1, 100);
          mydata[1] = constrain(heartRate, 1, 255);
          mydata[2] = devtemp >> 8;
          mydata[3] = devtemp;
          mydata[4] = voltage >> 8;
          mydata[5] = voltage;

          LoRaWAN.sendPacket(mydata, sizeof(mydata));
          
          Serial.print(F("Send HR="));
          Serial.print(heartRate, DEC);
          Serial.print(F(", SPO2="));
          Serial.print(spo2, DEC);
          Serial.print(F(", mcuTemp="));
          Serial.print(devtemp, DEC);
          Serial.print(F(", voltage="));
          Serial.println(voltage, DEC);
          
          //delay(15000);
          printReadings(String(spo2), String(heartRate));
          STM32L0.stop(180000);
      }
      
    }
  }
}

void printReadings(String spo2, String hr)
{
  display.begin();
  display.clearBuffer();
  
  display.setCursor(4, 4);
  display.setTextSize(2);
  display.setTextColor(COLOR1);
  display.setTextWrap(false);
  display.print("SpO2");

  display.setCursor(10, 26);
  display.setTextSize(6);
  display.print(spo2);

  display.setCursor(4, 84);
  display.setTextSize(2);
  display.print("Heart rate");

  display.setCursor(10, 106);
  display.setTextSize(6);
  display.print(hr);
  
  display.display();
}
