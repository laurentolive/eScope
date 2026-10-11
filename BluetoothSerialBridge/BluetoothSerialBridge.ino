// Example: ESP32 UART2 on GPIO17 (TX) and GPIO18 (RX)
// 3V3		=> TC2050.1 	=> red 		=> 3.3V  (remove if use power bank) 
// GND		=> TC2050.3 	=> black	=> GND  
// UART_DBG_TX 	=> TC2050.6	=> jaune	=> GPIO16 RX2 
// UART_DBG_RX 	=> TC2050.8	=> orange	=> GPIO17 TX2 
// to get BluetoothSerial.h : 
// - arduino-cli config set network.connection_timeout 1200s
// - arduino-cli core install esp32:esp32@3.3.11
// board upessy ESP32 wroom dev kit COM14

#define RXD2 16
#define TXD2 17
#define LED 2

#include "BluetoothSerial.h"

BluetoothSerial SerialBT;

void setup() {
  // Start default Serial (USB to PC)
  Serial.begin(115200);
  while (!Serial) {
    ; // wait for Serial to connect
  }
  Serial.println("ESP32 Bluetooth Serial Bridge started");

  // Start UART2 with custom pins
  Serial2.begin(115200, SERIAL_8N1, RXD2, TXD2);
  SerialBT.begin("ESP32_BT");  // Bluetooth device name
  pinMode (LED, OUTPUT);
}

void loop() {
  // Send data from UART2

  // If UART2 receives data, forward to USB Serial
  if (Serial2.available()) {
    char data = Serial2.read();
    SerialBT.write(data);
    Serial.write(data);
  }
  if (SerialBT.available()) 
  {
    char data = SerialBT.read();
    Serial2.write(data);
    Serial.write(data);
  }
  if (Serial.available()) {
  digitalWrite (LED, HIGH);	// turn on the LED
    char data = Serial.read();
    Serial2.write(data);
    SerialBT.write(data);
  digitalWrite (LED, LOW);	// turn on the LED
  }
}
