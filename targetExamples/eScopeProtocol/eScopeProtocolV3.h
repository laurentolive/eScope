/*******************************************************************************
 *
 *              #####   File : eScopeProtocolV3.h
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
 *    ##         ######  refer to c file
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#ifndef ESCOPE_PROTOCOL_V3_H_
#define ESCOPE_PROTOCOL_V3_H_

#include <stdint.h>
#include <stdbool.h>
#include "eScopeTransport.h"

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
    uint8_t* pu8Cfg;  /* pointer to persistent config buffer (EEPROM_SIZE bytes) */
} eScopeProtocolCfg_t;

typedef struct {
    eScopeTransport_t sTransport;
    uint8_t*  pu8Cfg;
    uint8_t   tu8Tx[ESCOPE_PROTOCOL_TX_SIZE];
    uint8_t   u8FrameCnt;
    uint8_t   u8TxSz;
    uint8_t   u8CfgSz;
    uint8_t   u8RxIdx;
    uint8_t   u8RxByte;
    uint8_t   u8Time;
} eScopeProtocol_t;

/*******************************************************************************
 * Public API
 ******************************************************************************/


#endif /* ESCOPE_PROTOCOL_V3_H_ */
