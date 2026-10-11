/*******************************************************************************
 *
 *              #####   File : eScopeTransportRtt.c
 *              #####
 *              #####   RTT transport — SEGGER RTT implementation
 *              #####
 *      #############   Compile with ESCOPE_TRANSPORT_RTT
 *   ################
 *  ######     ######   RX model : polling via eScopeTransportRtt_Poll().
 *  ######      #####              eScopeTransport_Read arms a pending read;
 *  *##########            Poll() drains the RTT DOWN buffer and fires the
 *    ##############        callback once all requested bytes are available.
 *         ###########      Partial reads are accumulated across Poll() calls.
 *    ##         ######
 *  ######        #####  TX model : SEGGER_RTT_Write (non-blocking ring buffer).
 *  ######       ######             EVT_TX fires immediately — the ring buffer
 *                                  is always ready to accept the next frame.
 *
 ******************************************************************************/

#include "eScopeTransport.h"
#ifdef ESCOPE_TRANSPORT_RTT

/*******************************************************************************
 * eScopeTransport common API
 ******************************************************************************/

void eScopeTransport_Init(eScopeTransport_t*          pThis,
                          const eScopeTransportCfg_t* pCfg,
                          void (*pfCB)(uint32_t, void*),
                          void*                       pvCtxt)
{
    pThis->uBufferIndex = pCfg->u8Id;
    pThis->pfCB         = pfCB;
    pThis->pvCtxt       = pvCtxt;
    pThis->pRxBuf       = NULL;
    pThis->u8RxLen      = 0U;
    pThis->bRxPending   = false;
    /* SEGGER_RTT_Init() is a no-op with static initialisation — call it
       anyway for forward-compatibility with future RTT versions. */
    SEGGER_RTT_Init();
}

void eScopeTransport_DeInit(eScopeTransport_t* pThis)
{
    pThis->bRxPending = false;
}

void eScopeTransport_Read(eScopeTransport_t* pThis, uint8_t* pBuf, uint8_t u8Len)
{
    /* Arm a pending read — Poll() fires the callback once all bytes arrive */
    pThis->pRxBuf     = pBuf;
    pThis->u8RxLen    = u8Len;
    pThis->bRxPending = true;
}

void eScopeTransport_Write(eScopeTransport_t* pThis, const uint8_t* pData, uint16_t u16Len)
{
    SEGGER_RTT_Write(pThis->uBufferIndex, pData, u16Len);
    /* RTT write is synchronous (data committed to ring buffer immediately),
       so the TX-complete event fires right after. */
    if (pThis->pfCB != NULL) {
        pThis->pfCB(ESCOPE_TRANSPORT_EVT_TX, pThis->pvCtxt);
    }
}

/*******************************************************************************
 * RTT-specific poll — call from main loop BEFORE eScopeProtocol_Process()
 *
 *  while (1) {
 *      eScopeTransportRtt_Poll(&sTransport);
 *      eScopeProtocol_Process(&sProto);
 *  }
 *
 * Checks availability without consuming first; only reads when all requested
 * bytes have arrived so the ring buffer is never partially drained.
 ******************************************************************************/

void eScopeTransportRtt_Poll(eScopeTransportRtt_t* pThis)
{
    if (!pThis->bRxPending) {
        return;
    }

    if (SEGGER_RTT_GetAvailReadSpace(pThis->uBufferIndex) < (unsigned)pThis->u8RxLen) {
        return;
    }

    SEGGER_RTT_Read(pThis->uBufferIndex, pThis->pRxBuf, (unsigned)pThis->u8RxLen);

    pThis->bRxPending = false;
    if (pThis->pfCB != NULL) {
        pThis->pfCB(ESCOPE_TRANSPORT_EVT_RX, pThis->pvCtxt);
    }
}

#endif /* ESCOPE_TRANSPORT_RTT */
