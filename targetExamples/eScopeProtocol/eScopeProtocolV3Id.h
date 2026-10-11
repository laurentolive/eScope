/*******************************************************************************
 *
 *              #####   File : eScopeProtocolV3Id.h
 *              #####
 *              #####   Function : refer to c file
 *              #####
 *      #############   Specification : refer to c file
 *   ################
 *  ######     ######   Test report : refer to c file
 *  ######      #####
 *  *##########         Abstract
 *    ##############    --------------------------------------------------------
 *         ###########
 *    ##         ######  ProtocolV3 variant with an embedded config buffer
 *  ######        #####  (u8CfgSz / u8TxSz held in eScopeProtocolCfg_t).
 *  ######       ######  Works with any transport (UART, RTT, TCP …).
 *
 ******************************************************************************/

#ifndef ESCOPE_PROTOCOL_V3ID_H_
#define ESCOPE_PROTOCOL_V3ID_H_

#include <stdint.h>
#include <stdbool.h>
#include "eScopeTransport.h"  /* for eScopeTransportCfg_t — must be UART */

/*******************************************************************************
 * Configuration — define in eScopeCfg.h
 ******************************************************************************/

#ifndef ESCOPE_PROTOCOL_CHAN_MAX
    #error "ESCOPE_PROTOCOL_CHAN_MAX must be defined in eScopeCfg.h"
#endif
#ifndef ESCOPE_PROTOCOL_ADDRESS_SIZE
    #error "ESCOPE_PROTOCOL_ADDRESS_SIZE must be defined in eScopeCfg.h"
#endif
#ifndef ESCOPE_PROTOCOL_DATASIZE_SIZE
    #error "ESCOPE_PROTOCOL_DATASIZE_SIZE must be defined in eScopeCfg.h"
#endif
#ifndef ESCOPE_PROTOCOL_TX_SIZE
    #error "ESCOPE_PROTOCOL_TX_SIZE must be defined in eScopeCfg.h"
#endif

/* reception timeout (in eScopeProtocol_Process calls) after which an incomplete
 * command is dropped */
#ifndef ESCOPE_PROTOCOL_RX_TIMEOUT
    #define ESCOPE_PROTOCOL_RX_TIMEOUT (10U)
#endif

#define ESCOPE_PROTOCOL_EEPROM_SIZE \
    (ESCOPE_PROTOCOL_CHAN_MAX * (ESCOPE_PROTOCOL_ADDRESS_SIZE + ESCOPE_PROTOCOL_DATASIZE_SIZE))

/*******************************************************************************
 * Type declarations
 ******************************************************************************/

typedef struct {
    uint8_t tu8Cfg[ESCOPE_PROTOCOL_EEPROM_SIZE];
    uint8_t u8CfgSz;
    uint8_t u8TxSz;
} eScopeProtocolCfg_t;

typedef struct {
    eScopeTransport_t      sTransport;
    eScopeProtocolCfg_t*   psCfg;
    uint8_t                tu8Tx[ESCOPE_PROTOCOL_TX_SIZE];
    uint8_t                u8FrameCnt;
    uint8_t                u8RxIdx;
    uint8_t                u8RxByte;
    uint8_t                u8Time;
} eScopeProtocol_t;

/*******************************************************************************
 * Public API
 ******************************************************************************/

#endif /* ESCOPE_PROTOCOL_V3ID_H_ */
