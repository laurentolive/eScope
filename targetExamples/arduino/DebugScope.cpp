/*******************************************************************************
 *  DebugScope.cpp — Arduino port of debugScopeV3
 *
 *  Différences vs STM32 original :
 *   - hwUart_* remplacé par HardwareSerial
 *   - CRITICAL_SECTION → noInterrupts()/interrupts()
 *   - Fin de trame détectée par silence sur millis() au lieu du flag u8Time
 *     incrémenté par timer (même sémantique, implémentation différente)
 *   - Pas de callback UART : polling dans update() + serialEvent() optionnel
 *   - Types STM32 (u8/u32/Bool) remplacés par stdint + bool
 ******************************************************************************/

#include "DebugScope.h"
#include <string.h>

// ── Constructeur ─────────────────────────────────────────────────────────────
DebugScope::DebugScope(HardwareSerial &stream, uint32_t baudrate)
    : _serial(stream)
    , _baudrate(baudrate)
    , _frameCnt(0)
    , _txSz(0)
    , _cfgSz(0)
    , _rxIdx(0)
    , _lastRxMs(0)
    , _ended(false)
{
    memset(_cfg,   0, sizeof(_cfg));
    memset(_txBuf, 0, sizeof(_txBuf));
}

// ── begin() ──────────────────────────────────────────────────────────────────
void DebugScope::begin()
{
    _serial.begin(_baudrate);
    memset(_cfg,   0, sizeof(_cfg));
    _frameCnt     = 0;
    _txSz         = 0;
    _cfgSz        = 0;
    _rxIdx        = 0;
    _ended        = false;
}

// ── end() ────────────────────────────────────────────────────────────────────
void DebugScope::end()
{
    _serial.end();
    _ended = true;
}

// ── rxByte() — appelé depuis update() (polling) ou serialEvent() ─────────────
//  Equivalent du case HWUART_EVENT_RX_COMPLETE dans le callback STM32
void DebugScope::rxByte(uint8_t b)
{
    noInterrupts();
    if (_rxIdx < sizeof(_cfg))
    {
        _cfg[_rxIdx] = b;
        _rxIdx++;
    }
    _lastRxMs = millis(); // rafraîchit le timestamp pour la détection silence
    interrupts();
}

// ── update() — à appeler dans loop() ─────────────────────────────────────────
//  Equivalent de debugScope_Process()
void DebugScope::update()
{
    // ── 1. Polling RX (si serialEvent() n'est pas utilisé) ───────────────────
    while (_serial.available())
    {
        rxByte((uint8_t)_serial.read());
    }

    // ── 2. Silence sur la ligne → traitement des commandes reçues ────────────
    noInterrupts();
    uint8_t  rxIdx  = _rxIdx;
    uint32_t lastRx = _lastRxMs;
    interrupts();

    if ((rxIdx > _cfgSz) && ((millis() - lastRx) >= DEBUG_SCOPE_RX_SILENCE_MS))
    {
        _processCommand();
    }

    // ── 3. Envoi trame TX ────────────────────────────────────────────────────
    if (_cfg[0] & ERR_FLAG)
    {
        _sendErrorFrame(_cfg[0]);
    }
    else if (_cfgSz > 0)
    {
        _sendDataFrame();
    }
}

// ── _remove() ────────────────────────────────────────────────────────────────
//  Retire len octets à idx, la suite (commandes et octets reçus) est décalée :
//  l'ordre des lectures, donc le format de trame attendu par le PC, est gardé.
//  À appeler interruptions masquées.
void DebugScope::_remove(uint8_t idx, uint8_t len)
{
    uint8_t end = idx + len;

    memmove(&_cfg[idx], &_cfg[end], _rxIdx - end);
    _rxIdx -= len;
    memset(&_cfg[_rxIdx], 0, len);
}

// ── _rxDrop() — abandonne les octets reçus non traités ───────────────────────
void DebugScope::_rxDrop()
{
    noInterrupts();
    memset(&_cfg[_cfgSz], 0, _rxIdx - _cfgSz);
    _rxIdx = _cfgSz;
    interrupts();
}

// ── _error() — erreur envoyée au PC jusqu'au prochain CMD_CLEAR ──────────────
void DebugScope::_error(uint8_t errCode)
{
    _rxDrop();
    _cfg[0] = errCode;
}

// ── _processCommand() ─────────────────────────────────────────────────────────
//  Traite toutes les commandes reçues
void DebugScope::_processCommand()
{
    while (_cfgSz < _rxIdx)
    {
        uint8_t *pu8Rx   = &_cfg[_cfgSz];
        uint8_t  avail   = _rxIdx - _cfgSz;
        bool     full    = (_rxIdx >= sizeof(_cfg));
        bool     timeout = ((millis() - _lastRxMs) >= DEBUG_SCOPE_RX_TIMEOUT_MS);

        switch (pu8Rx[CMD_IDX])
        {
            // ── Quitter ──────────────────────────────────────────────────────────
            case CMD_DEINIT:
                _ended = true;
                // fall through : reset

            // ── Nouvelle config : ce qui a été reçu avec est abandonné ──────────
            case CMD_CLEAR:
                noInterrupts();
                memset(_cfg, 0, sizeof(_cfg));
                _cfgSz = 0;
                _rxIdx = 0;
                interrupts();
                _txSz  = 0;
                return;

            // ── Ajouter une variable à lire (périodique ou one-shot) ─────────────
            case CMD_READ_APERIODIC:
            case CMD_READ_PERIODIC:
                if (avail < CMD_FRAME_SIZE) // commande incomplète
                {
                    if (full || ((_cfgSz + CMD_FRAME_SIZE) > DEBUG_SCOPE_EEPROM_SIZE))
                        _error(ERR_CFGOVF);
                    else if (timeout)
                        _rxDrop();
                    return;
                }
                if ((_cfgSz + CMD_FRAME_SIZE) > DEBUG_SCOPE_EEPROM_SIZE)
                {
                    _error(ERR_CFGOVF);  // config pleine
                    return;
                }
                if ((_txSz + pu8Rx[CMD_SIZE_IDX]) >
                    (DEBUG_SCOPE_TX_SIZE - HEADER_SIZE - FOOTER_SIZE))
                {
                    _error(ERR_TXOVF);   // TX buffer plein
                    return;
                }
                if (_txSz > DEBUG_SCOPE_TX_MAX)
                {
                    _error(ERR_BWOVF);   // bandwidth overflow
                    return;
                }
                _txSz  += pu8Rx[CMD_SIZE_IDX];
                _cfgSz += CMD_FRAME_SIZE;
                break;

            // ── Écriture directe en RAM (ex: modifier un paramètre) ──────────────
            case CMD_WRITE_APERIODIC:
            {
                uint16_t len = CMD_FRAME_SIZE;

                if (avail >= CMD_FRAME_SIZE) len += pu8Rx[CMD_SIZE_IDX];
                if (avail < len) // incomplète : abandonnée si elle ne peut pas tenir
                {
                    if (full || timeout) _rxDrop();
                    return;
                }

                // Reconstruction de l'adresse 32 bits (little-endian, comme STM32)
                uintptr_t addr = (uintptr_t)(((uint32_t)pu8Rx[CMD_ADDR_IDX + 0] <<  0)
                                           | ((uint32_t)pu8Rx[CMD_ADDR_IDX + 1] <<  8)
                                           | ((uint32_t)pu8Rx[CMD_ADDR_IDX + 2] << 16)
                                           | ((uint32_t)pu8Rx[CMD_ADDR_IDX + 3] << 24));
                uint8_t *dst = (uint8_t *)addr;
                uint8_t *src = &pu8Rx[CMD_DATA_IDX];
                uint8_t  n   = pu8Rx[CMD_SIZE_IDX];
                while (n--) { *dst++ = *src++; }

                // Commande one-shot : on la retire du buffer
                noInterrupts();
                _remove(_cfgSz, (uint8_t)len);
                interrupts();
                break;
            }

            // ── NOP / commande inconnue : resynchro sur la prochaine réception ──
            default:
                _rxDrop();
                return;
        }
    }
}

// ── _sendErrorFrame() ────────────────────────────────────────────────────────
void DebugScope::_sendErrorFrame(uint8_t errCode)
{
    _txBuf[0] = HEADER_SYNC_BYTE;
    _txBuf[1] = _frameCnt++;
    _txBuf[2] = 0;           // data size = 0
    _txBuf[3] = errCode;
    _txBuf[4] = FOOTER_SYNC_BYTE;
    _serial.write(_txBuf, 5);
}

// ── _sendDebugFrame() ────────────────────────────────────────────────────────
void DebugScope::_sendDebugFrame(uint8_t* pbuf, uint8_t u8Sz)
{
    _txBuf[0] = HEADER_SYNC_BYTE;
    _txBuf[1] = _frameCnt++;
    _txBuf[2] = u8Sz;
    _txBuf[3] = CMD_DEBUG;
    memcpy(&_txBuf[4], pbuf, u8Sz);
    _txBuf[4+u8Sz] = FOOTER_SYNC_BYTE;
    _serial.write(_txBuf, 5+u8Sz);
}

// ── _sendDataFrame() ─────────────────────────────────────────────────────────
//  Copie la mémoire de toutes les lectures, puis retire les lectures one-shot
void DebugScope::_sendDataFrame()
{
    _txBuf[0] = HEADER_SYNC_BYTE;
    _txBuf[1] = _frameCnt++;
    _txBuf[2] = _txSz;
    _txBuf[3] = CMD_READ_PERIODIC;

    uint8_t *pu8Tx = &_txBuf[HEADER_SIZE];

    for (uint8_t idx = 0; idx < _cfgSz; idx += CMD_FRAME_SIZE)
    {
        uint8_t *pu8Cfg = &_cfg[idx];
        // Reconstruction adresse 32 bits
        uintptr_t addr = (uintptr_t)(((uint32_t)pu8Cfg[CMD_ADDR_IDX + 0] <<  0)
                                   | ((uint32_t)pu8Cfg[CMD_ADDR_IDX + 1] <<  8)
                                   | ((uint32_t)pu8Cfg[CMD_ADDR_IDX + 2] << 16)
                                   | ((uint32_t)pu8Cfg[CMD_ADDR_IDX + 3] << 24));
        uint8_t *src    = (uint8_t *)addr;
        uint8_t *srcEnd = src + pu8Cfg[CMD_SIZE_IDX];

        while (src < srcEnd) { *pu8Tx++ = *src++; }
    }

    *pu8Tx = FOOTER_SYNC_BYTE;
    _serial.write(_txBuf, _txSz + HEADER_SIZE + FOOTER_SIZE);

    // Lectures one-shot servies : retirées, l'ordre des suivantes est gardé
    noInterrupts();
    uint8_t idx = 0;
    while (idx < _cfgSz)
    {
        if (_cfg[idx + CMD_IDX] == CMD_READ_APERIODIC)
        {
            _txSz  -= _cfg[idx + CMD_SIZE_IDX];
            _cfgSz -= CMD_FRAME_SIZE;
            _remove(idx, CMD_FRAME_SIZE);
        }
        else
        {
            idx += CMD_FRAME_SIZE;
        }
    }
    interrupts();
}
