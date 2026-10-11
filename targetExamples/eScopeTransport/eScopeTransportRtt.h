/*******************************************************************************
 *
 *              #####   File : eScopeTransportRtt.h
 *              #####
 *              #####   RTT transport — SEGGER J-Link RTT concrete types
 *              #####
 *      #############   Do not include directly — include eScopeTransport.h
 *   ################   with ESCOPE_TRANSPORT_RTT defined.
 *  ######     ######
 *  ######      #####   Requires: SEGGER_RTT.h / SEGGER_RTT.c in your build.
 *  *##########            See targetExamples/SEGGER_RTT/ for a reference copy.
 *    ##############
 *         ###########  RX model : polling — call eScopeTransportRtt_Poll()
 *    ##         ######  from your main loop BEFORE eScopeProtocol_Process().
 *  ######        #####
 *  ######       ######   while (1) {
 *                             eScopeTransportRtt_Poll(&sTransport);
 *                             eScopeProtocol_Process(&sProto);
 *                         }
 *
 *                        TX model : SEGGER_RTT_Write (non-blocking ring buffer).
 *                        The TX callback fires immediately after each write
 *                        since the ring buffer is always ready to accept data.
 *
 ******************************************************************************/

#ifndef ESCOPE_TRANSPORT_RTT_H_
#define ESCOPE_TRANSPORT_RTT_H_

#include <stdint.h>
#include <stdbool.h>
#include "SEGGER_RTT.h"

/*******************************************************************************
 * RTT transport handle
 ******************************************************************************/

typedef struct {
    unsigned  uBufferIndex;                      /* RTT channel index (typically 0) */
    void    (*pfCB)(uint32_t event, void* pvCtxt);
    void     *pvCtxt;
    uint8_t  *pRxBuf;       /* pending read buffer (set by eScopeTransport_Read) */
    uint8_t   u8RxLen;      /* bytes to receive                                  */
    bool      bRxPending;   /* true = read armed, waiting for data                */
} eScopeTransportRtt_t;

/*******************************************************************************
 * RTT init configuration
 ******************************************************************************/

typedef struct {
    unsigned u8Id;  /* RTT channel index — use 0 for the default terminal */
} eScopeTransportRtt_Cfg_t;

/*******************************************************************************
 * RTT-specific poll — MUST be called from the main loop BEFORE Process()
 ******************************************************************************/

void eScopeTransportRtt_Poll(eScopeTransportRtt_t* pThis);

#endif /* ESCOPE_TRANSPORT_RTT_H_ */
