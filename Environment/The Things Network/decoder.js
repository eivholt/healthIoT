function Decoder(bytes, port) {
    // Decode an uplink message from a buffer
    // (array) of bytes to an object of fields.
    var decoded = {};
  
    if (port === 1) 
    {
      decoded.spo2 = bytes[0];
      decoded.hr = bytes[1];
      decoded.devtemp = (bytes[2]<<8 | bytes[3])/100;
      decoded.voltage = 3000;
    }
  
    return decoded;
  }