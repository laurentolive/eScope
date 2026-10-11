/*******************************************************************************
 *  DebugScope.h — Arduino port of debugScopeV3
 *
 *  Architecture : le scope.exe lit le .elf/.map, envoie des commandes avec
 *  adresses mémoire + tailles, le firmware copie la RAM brute et renvoie.
 *  Le MCU est complètement passif vis-à-vis du choix des variables.
 *
 *  Compatibilité : AVR (Uno/Mega), STM32duino, ESP32, SAMD
 ******************************************************************************/

#ifndef DEBUG_SCOPE_H
#define DEBUG_SCOPE_H

#include <Arduino.h>

// ── Configuration (modifiable) ───────────────────────────────────────────────
#define DEBUG_SCOPE_BAUDRATE       (115200U)
#define DEBUG_SCOPE_CHAN_MAX        (8U)
#define DEBUG_SCOPE_TX_SIZE        (128U)
// EEPROM = espace config : 8 canaux × 6 octets (CMD+SIZE+ADDR32)
#define DEBUG_SCOPE_EEPROM_SIZE    (DEBUG_SCOPE_CHAN_MAX * 6U)
// Timeout silence UART pour détecter fin de trame (ms)
#define DEBUG_SCOPE_RX_SILENCE_MS  (2U)
// Commande incomplète abandonnée après ce silence (ms)
#define DEBUG_SCOPE_RX_TIMEOUT_MS  (20U)
// Période d'appel de update() = "Sampling Time" du device eScope.
// 115200 bauds ≈ 11,5 octets/ms : à 5 ms, ~52 octets de données par trame.
#define DEBUG_SCOPE_TS (0.005) // 5 ms
#define DEBUG_SCOPE_TX_MAX ((char)(DEBUG_SCOPE_BAUDRATE/10*DEBUG_SCOPE_TS))

// ── Protocole (privé mais exposé pour tests) ─────────────────────────────────
#define ERR_FLAG   (0x80)
#define READ_FLAG  (0x40)
#define WRITE_FLAG (0x20)

#define CMD_NOP              (0x00)
#define CMD_DEINIT           (0x01)
#define CMD_CLEAR            (0x02)
#define CMD_READ_PERIODIC    (0x03 | READ_FLAG)
#define CMD_READ_APERIODIC   (0x04 | READ_FLAG)
#define CMD_WRITE_APERIODIC  (0x05 | WRITE_FLAG)
#define CMD_DEBUG            (0x06)
#define ERR_TXOVF            (0x06 | ERR_FLAG)
#define ERR_CFGOVF           (0x07 | ERR_FLAG)
#define ERR_CMDUNK           (0x08 | ERR_FLAG)
#define ERR_BWOVF            (0x09 | ERR_FLAG)

#define CMD_FRAME_SIZE  (6U)   // CMDx1 + SIZEx1 + ADDRx4
#define CMD_IDX         (0U)
#define CMD_SIZE_IDX    (1U)
#define CMD_ADDR_IDX    (2U)
#define CMD_DATA_IDX    (6U)

#define HEADER_SIZE      (4U)
#define FOOTER_SIZE      (1U)
#define HEADER_SYNC_BYTE (0x55)
#define FOOTER_SYNC_BYTE (0xAA)

// ── Classe principale ────────────────────────────────────────────────────────
class DebugScope {
public:
    /**
     * @param stream   Port série (Serial, Serial1, Serial2…)
     * @param baudrate Vitesse (défaut 115200)
     */
    explicit DebugScope(HardwareSerial &stream,
                        uint32_t baudrate = DEBUG_SCOPE_BAUDRATE);

    /**
     * Initialise le port série et remet à zéro l'état interne.
     * À appeler dans setup().
     */
    void begin();

    /**
     * Arrête proprement le scope (répond à CMD_DEINIT).
     */
    void end();

    /**
     * Traitement principal — à appeler dans loop() aussi souvent que possible,
     * ou depuis un timer ISR (attention : pas d'appels bloquants dedans).
     *
     * Equivalent de debugScope_Process() :
     *  - détecte fin de trame RX par silence
     *  - exécute la commande reçue
     *  - construit et envoie la trame TX
     */
    void update();

    /**
     * Retourne true si CMD_DEINIT a été reçu (scope fermé côté PC).
     */
    bool isEnded() const { return _ended; }

    /**
     * Reçoit un octet depuis l'ISR série ou depuis update() (polling).
     * Public pour pouvoir être appelé depuis serialEvent().
     */
    void rxByte(uint8_t b);

private:
    // ── Helpers ──────────────────────────────────────────────────────────────
    void _processCommand();
    void _sendErrorFrame(uint8_t errCode);
    void _sendDataFrame();
    void _remove(uint8_t idx, uint8_t len);
    void _rxDrop();
    void _error(uint8_t errCode);
    void _sendDebugFrame(uint8_t* pbuf, uint8_t u8Sz);

    // ── Membres ──────────────────────────────────────────────────────────────
    HardwareSerial &_serial;
    uint32_t        _baudrate;

    // Buffer de configuration :
    //   _cfg[0 .. _cfgSz[       commandes de lecture actives (périodiques et one-shot)
    //   _cfg[_cfgSz .. _rxIdx[  octets reçus pas encore traités
    uint8_t  _cfg[DEBUG_SCOPE_EEPROM_SIZE * 2];

    uint8_t  _txBuf[DEBUG_SCOPE_TX_SIZE];
    uint8_t  _dbgBuf[4];
    uint8_t  _frameCnt;   // compteur de trame (flow control)
    uint8_t  _txSz;       // taille des données à envoyer
    uint8_t  _cfgSz;      // octets valides dans _cfg[]

    // Réception
    volatile uint8_t  _rxIdx;          // index d'écriture dans _cfg[]
    volatile uint32_t _lastRxMs;       // timestamp du dernier octet reçu

    bool     _ended;
};

#endif // DEBUG_SCOPE_H
