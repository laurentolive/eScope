/*******************************************************************************
 *
 *              #####   File : eScopeTransportUart.c
 *              #####
 *              #####   UART transport — Renesas RA FSP implementation (FPB-RA4E1)
 *              #####
 *      #############   Compiled when ESCOPE_TRANSPORT_UART is defined in eScopeCfg.h.
 *   ################
 *  ######     ######   FSP configuration (Stacks tab of configuration.xml):
 *  ######      #####     New Stack > Connectivity > UART (r_sci_uart)
 *  *##########             Name     : g_uart9      Channel : 9
 *    ##############        Baud rate: 115200       Callback: eScope_uart_callback
 *         ###########    Pins: P109 = TXD9, P110 = RXD9 (Pmod 2 / Arduino header)
 *    ##         ######
 *  ######        #####   RX model : FSP interrupt, UART_EVENT_RX_CHAR (one byte, no
 *  ######       ######              pending R_SCI_UART_Read needed).
 *                        TX model : R_SCI_UART_Write (non-blocking: one frame per
 *                                   sampling period must fit in the period).
 *
 ******************************************************************************/

#include "hal_data.h"
#include "eScopeTransport.h"
#ifdef ESCOPE_TRANSPORT_UART

static eScopeTransport_t* s_pThis;      /* single eScope UART per firmware      */
static uint8_t*           s_pu8RxBuf;   /* armed by eScopeTransport_Read        */

/*******************************************************************************
 * eScopeTransport common API (u8Id is not used: g_uart9 is the eScope UART)
 ******************************************************************************/

void eScopeTransport_Init(eScopeTransport_t*          pThis,
                          const eScopeTransportCfg_t* pCfg,
                          void (*pfCB)(uint32_t, void*),
                          void* pvCtxt)
{
    pThis->u8Id   = pCfg->u8Id;
    pThis->pfCB   = pfCB;
    pThis->pvCtxt = pvCtxt;
    s_pThis       = pThis;
    s_pu8RxBuf    = NULL;

    fsp_err_t err = g_uart9.p_api->open(g_uart9.p_ctrl, g_uart9.p_cfg);
    ASSERT(FSP_SUCCESS == err);
    (void)err;
}

void eScopeTransport_DeInit(eScopeTransport_t* pThis)
{
    (void)pThis;
    s_pu8RxBuf = NULL;
    g_uart9.p_api->close(g_uart9.p_ctrl);
}

void eScopeTransport_Read(eScopeTransport_t* pThis, uint8_t* pBuf, uint8_t u8Len)
{
    (void)pThis;
    ASSERT(u8Len == 1U);                /* the protocol reads one byte at a time */
    (void)u8Len;
    s_pu8RxBuf = pBuf;
}

void eScopeTransport_Write(eScopeTransport_t* pThis, const uint8_t* pData, uint16_t u16Len)
{
    (void)pThis;
    g_uart9.p_api->write(g_uart9.p_ctrl, pData, u16Len);
}

void eScopeTransportUart_Poll(eScopeTransportUart_t* pThis)
{
    (void)pThis;   /* interrupt-driven: nothing to poll */
}

/*******************************************************************************
 * FSP UART callback (name set in the r_sci_uart stack properties)
 ******************************************************************************/

void eScope_uart_callback(uart_callback_args_t* p_args)
{
    if ((p_args->event == UART_EVENT_RX_CHAR) && (s_pu8RxBuf != NULL) && (s_pThis->pfCB != NULL))
    {
        *s_pu8RxBuf = (uint8_t)p_args->data;
        s_pu8RxBuf  = NULL;             /* re-armed by the protocol from pfCB */
        s_pThis->pfCB(ESCOPE_TRANSPORT_EVT_RX, s_pThis->pvCtxt);
    }
}

#endif /* ESCOPE_TRANSPORT_UART */
