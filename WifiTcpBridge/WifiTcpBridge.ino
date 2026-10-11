// Serial to Wi-Fi TCP bridge for eScope (eScope device: TCP, Address = IP printed below, Port = 3333)
// ESP32 UART2 on GPIO16 (RX2) and GPIO17 (TX2), 115200 8N1, 3.3 V levels
//   target TX  => GPIO16 RX2
//   target RX  => GPIO17 TX2
//   target GND => GND
// The ESP32 joins the Wi-Fi network (station) and serves one TCP client at a time.
// Its IP address is printed on the USB serial port (115200) at startup.
// arduino-cli core install esp32:esp32
// board: ESP32 Dev Module (esp32:esp32:esp32)

#include <WiFi.h>

#define RXD2 16
#define TXD2 17
#define LED 2

const char* WIFI_SSID     = "your-ssid";
const char* WIFI_PASSWORD = "your-password";
const uint16_t TCP_PORT   = 3333;

WiFiServer server(TCP_PORT);
WiFiClient client;

void setup() {
  // Default Serial (USB to PC): status messages
  Serial.begin(115200);
  Serial2.begin(115200, SERIAL_8N1, RXD2, TXD2);
  pinMode(LED, OUTPUT);

  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);  // lower latency
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  Serial.print("Connecting to ");
  Serial.println(WIFI_SSID);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println();
  Serial.print("eScope TCP bridge ready: ");
  Serial.print(WiFi.localIP());
  Serial.print(":");
  Serial.println(TCP_PORT);

  server.begin();
  server.setNoDelay(true);
}

void loop() {
  // accept a new client (eScope), replacing the previous one
  if (server.hasClient()) {
    if (client) client.stop();
    client = server.accept();
    client.setNoDelay(true);
    while (Serial2.available()) Serial2.read();  // drop data received while nobody listened
    Serial.print("Client ");
    Serial.println(client.remoteIP());
  }
  digitalWrite(LED, client.connected() ? HIGH : LOW);

  uint8_t buf[256];
  // target => eScope
  size_t n = Serial2.available();
  if (n > 0) {
    n = Serial2.readBytes(buf, min(n, sizeof(buf)));
    if (client.connected()) client.write(buf, n);
  }
  // eScope => target
  if (client.connected() && client.available()) {
    n = client.read(buf, sizeof(buf));
    if (n > 0) Serial2.write(buf, n);
  }
}
