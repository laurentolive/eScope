/*******************************************************************************
 *  basic_scope.ino — exemple d'utilisation de DebugScope
 *
 *  Câblage : scope.exe connecté sur Serial (TX1/RX1)
 *  Serial0 reste libre pour debug humain.
 *
 *  Le firmware ne connaît PAS les variables à envoyer :
 *  scope.exe lit le .elf/.map, envoie les adresses RAM,
 *  et le MCU copie la mémoire brute.
 ******************************************************************************/

#include "DebugScope.h"

// ── Variables applicatives quelconques ──────────────────────────────────────
// scope.exe les trouvera dans le .elf par leur symbole
volatile float vitesse;
volatile float courant;
int32_t position;
uint16_t errFlags = 0x0000;

// ── Instance scope ───────────────────────────────────────────────────────────
// Sur Uno : Serial est le seul port → impossible d'avoir debug + scope en même
// temps. Sur Mega/STM32/ESP32 : utiliser Serial, Serial2…
DebugScope scope(Serial);   // 115200 baud par défaut

// ── Simulation applicative ───────────────────────────────────────────────────
static uint32_t lastUpdate = 0;

void appUpdate()
{
    // Simule une sinusoïde + bruit — remplace par ton vrai code
    static float t = 0.0f;
    t += (float)DEBUG_SCOPE_TS;

    vitesse = 10.0f * sinf(2.0f * 3.14159f * 1.0f * t);  // 1Hz, ±10
    courant = 10.0f * sinf(2.0f * 3.14159f * 2.0f * t);  // 2Hz, ±10
    position++;

    if (position > 10000) position = -10000;
}

void setup()
{
    // Serial.begin(115200); // debug humain

    scope.begin();        // démarre le scope sur Serial

    // Serial.println(F("DebugScope ready."));
    // Serial.print(F("  &vitesse  = 0x")); Serial.println((uintptr_t)&vitesse,  HEX);
    // Serial.print(F("  &courant  = 0x")); Serial.println((uintptr_t)&courant,  HEX);
    // Serial.print(F("  &position = 0x")); Serial.println((uintptr_t)&position, HEX);
    // Serial.print(F("  &errFlags = 0x")); Serial.println((uintptr_t)&errFlags, HEX);
}

void loop()
{
    // ── Mise à jour applicative toutes les DEBUG_SCOPE_TS (5 ms) ─────────────
    uint32_t now = millis();
    if (now - lastUpdate >= (uint32_t)(DEBUG_SCOPE_TS * 1000))
    {
        lastUpdate += (uint32_t)(DEBUG_SCOPE_TS * 1000);
        appUpdate();
        scope.update();
    }
}

// ── Optionnel : serialEvent() sur la plateforme AVR ──────────────────────────
// Sur AVR, serialEvent() est appelé entre deux loop() si des octets sont dispo.
// Cela améliore la réactivité RX sans avoir besoin d'un vrai ISR UART.
// Sur ESP32/STM32, le polling dans update() est suffisant.
#if defined(ARDUINO_ARCH_AVR)
void serialEvent1()
{
    while (Serial.available())
    {
        scope.rxByte((uint8_t)Serial.read());
    }
}
#endif
