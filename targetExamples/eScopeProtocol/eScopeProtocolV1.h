/*******************************************************************************
 *
 *              #####   File : eScopeProtocolV1.h
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

#ifndef ESCOPE_PROTOCOL_V1_H_
#define ESCOPE_PROTOCOL_V1_H_

#include <stdint.h>
#include <stdbool.h>
#include "eScopeTransport.h"

/*******************************************************************************
 * Configuration — define in eScopeCfg.h
 ******************************************************************************/
#ifdef ESCOPE_PROTOCOL_V1

#ifndef ESCOPE_PROTOCOL_CHAN_MAX
    #error "ESCOPE_PROTOCOL_CHAN_MAX must be defined in eScopeCfg.h"
#endif
#ifndef ESCOPE_PROTOCOL_DATA_TYPE
    #error "ESCOPE_PROTOCOL_DATA_TYPE must be defined in eScopeCfg.h"
#endif
#ifndef ESCOPE_PROTOCOL_ADDRESS_SIZE
    #error "ESCOPE_PROTOCOL_ADDRESS_SIZE must be defined in eScopeCfg.h"
#endif

/*******************************************************************************
 * Derived constants — do not edit
 ******************************************************************************/

#define ESCOPE_PROTOCOL_DATA_SIZE    (sizeof(ESCOPE_PROTOCOL_DATA_TYPE))
#define ESCOPE_PROTOCOL_CFG_SIZE     (ESCOPE_PROTOCOL_CHAN_MAX * ESCOPE_PROTOCOL_ADDRESS_SIZE)
#define ESCOPE_PROTOCOL_PAYLOAD_SIZE (ESCOPE_PROTOCOL_CHAN_MAX * ESCOPE_PROTOCOL_DATA_SIZE)
#define ESCOPE_PROTOCOL_TX_SIZE      (4U + ESCOPE_PROTOCOL_PAYLOAD_SIZE + 1U)

/*******************************************************************************
 * Type declarations
 ******************************************************************************/

typedef struct {
    uint8_t tu8Address[ESCOPE_PROTOCOL_CFG_SIZE];
} eScopeProtocolCfg_t;

typedef struct {
    eScopeTransport_t       sTransport;
    uint8_t                 tu8Tx[ESCOPE_PROTOCOL_TX_SIZE];
    eScopeProtocolCfg_t*    psCfg;
    uint8_t                 u8RxIdx;
    uint8_t                 u8RxByte;
    uint8_t                 u8FrameCnt;
    uint8_t                 u8Time;
    bool                    bConfigured;
} eScopeProtocol_t;

/*******************************************************************************
 * Public API
 ******************************************************************************/

#endif /* ESCOPE_PROTOCOL_V1 */
#endif /* ESCOPE_PROTOCOL_V1_H_ */
