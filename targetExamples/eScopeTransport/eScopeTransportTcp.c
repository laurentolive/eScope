/*******************************************************************************
 *
 *              #####   File : eScopeTransportTcp.c
 *              #####
 *              #####   TCP transport — eScopeTransport_xxx implementation
 *              #####
 *      #############   Compile with -DESCOPE_TRANSPORT_TCP.
 *   ################   Link this file OR eScopeTransportUart.c, never both.
 *  ######     ######
 *  ######      #####   STUB — fill in platform socket calls.
 *  *##########         Callback model: arm an async receive in _Read(), fire
 *    ##############    ESCOPE_TRANSPORT_EVT_RX from your receive ISR/task;
 *         ###########  fire ESCOPE_TRANSPORT_EVT_TX from your send completion.
 *    ##         ######
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#include "eScopeTransport.h"
#ifdef ESCOPE_TRANSPORT_TCP

/*******************************************************************************
 * Public — eScopeTransport_xxx implementation (stubs)
 ******************************************************************************/

void eScopeTransport_Init(eScopeTransport_t*        pThis,
                          const eScopeTransportCfg_t* pCfg,
                          void (*pfCB)(uint32_t, void*),
                          void* pvCtxt)
{
    pThis->pfCB   = pfCB;
    pThis->pvCtxt = pvCtxt;
    pThis->fd     = -1;

    /* TODO: create socket, connect to pCfg->u32IpAddr : pCfg->u16Port,
             arm background receive task that fires pfCB(ESCOPE_TRANSPORT_EVT_RX, pvCtxt)
             on each received byte or chunk.                                    */
    (void)pCfg;
}

void eScopeTransport_DeInit(eScopeTransport_t* pThis)
{
    /* TODO: close socket, stop receive task */
    (void)pThis;
}

void eScopeTransport_Read(eScopeTransport_t* pThis, uint8_t* pBuf, uint8_t u8Len)
{
    /* TODO: arm async receive of u8Len bytes into pBuf */
    (void)pThis; (void)pBuf; (void)u8Len;
}

void eScopeTransport_Write(eScopeTransport_t* pThis, const uint8_t* pData, uint16_t u16Len)
{
    /* TODO: send u16Len bytes; fire pfCB(ESCOPE_TRANSPORT_EVT_TX) on completion */
    (void)pThis; (void)pData; (void)u16Len;
}

#endif /* ESCOPE_TRANSPORT_TCP */
