/*******************************************************************************
 *
 *              #####   File : simHw.c
 *              #####
 *              #####   Simulated board: DLL entry points, UART driver of the
 *              #####   eScope transport, memory sanity check
 *      #############
 *   ################   The UART "wire" is the host: transmitted bytes go to the
 *  ######     ######   simUartTx_t callback, received bytes come from
 *  ######      #####   sim_UartRx (the host relays both to TCP).
 *  *##########
 *    ##############
 *         ###########
 *    ##         ######
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include "eScopeTransport.h"
#include "simHw.h"

static simUartTx_t        gpfUartTx;
static eScopeTransport_t* gpsUart;    /* eScope transport on SIM_UART_ESCOPE */
static uint8_t*           gpu8Rx;     /* reception armed by eScopeTransport_Read */
static uint8_t            gu8RxLen;

/*******************************************************************************
 * DLL entry points
 ******************************************************************************/

SIM_API void sim_Init(simUartTx_t pfUartTx)
{
    gpfUartTx = pfUartTx;
    app_Init();
}

SIM_API void sim_Tick(void)
{
    app_Tick();
}

/* byte received while no reception is armed: lost (UART overrun) */
SIM_API void sim_UartRx(uint8_t u8Id, uint8_t u8Byte)
{
    if ((gpsUart == NULL) || (u8Id != gpsUart->u8Id) || (gpu8Rx == NULL)) return;

    *gpu8Rx++ = u8Byte;
    if (--gu8RxLen == 0U)
    {
        gpu8Rx = NULL;
        gpsUart->pfCB(ESCOPE_TRANSPORT_EVT_RX, gpsUart->pvCtxt);
    }
}

/*******************************************************************************
 * eScopeTransport common API (UART)
 ******************************************************************************/

void eScopeTransport_Init(eScopeTransport_t*          pThis,
                          const eScopeTransportCfg_t* pCfg,
                          void (*pfCB)(uint32_t, void*),
                          void* pvCtxt)
{
    pThis->u8Id   = pCfg->u8Id;
    pThis->pfCB   = pfCB;
    pThis->pvCtxt = pvCtxt;
    gpsUart       = pThis;
}

void eScopeTransport_DeInit(eScopeTransport_t* pThis)
{
    (void)pThis;
    gpu8Rx  = NULL;
    gpsUart = NULL;
}

void eScopeTransport_Read(eScopeTransport_t* pThis, uint8_t* pBuf, uint8_t u8Len)
{
    (void)pThis;
    gu8RxLen = u8Len;
    gpu8Rx   = (u8Len != 0U) ? pBuf : NULL;
}

void eScopeTransport_Write(eScopeTransport_t* pThis, const uint8_t* pData, uint16_t u16Len)
{
    if (gpfUartTx != NULL) gpfUartTx(pThis->u8Id, pData, u16Len);
    pThis->pfCB(ESCOPE_TRANSPORT_EVT_TX, pThis->pvCtxt);
}

void eScopeTransportUart_Poll(eScopeTransportUart_t* pThis)
{
    (void)pThis;   /* reception delivered by sim_UartRx */
}

/*******************************************************************************
 * Memory sanity check (ESCOPE_ADDR_IS_VALID): inside the loaded DLL image
 ******************************************************************************/

bool simHw_IsValid(const uint8_t* pu8, uint32_t u32Size)
{
    const uint8_t*          pu8Base = (const uint8_t*)&__ImageBase;
    const IMAGE_DOS_HEADER* psDos   = (const IMAGE_DOS_HEADER*)pu8Base;
    const IMAGE_NT_HEADERS* psNt    = (const IMAGE_NT_HEADERS*)(pu8Base + psDos->e_lfanew);

    return (pu8 >= pu8Base) && ((uintptr_t)(pu8 - pu8Base) + u32Size <= psNt->OptionalHeader.SizeOfImage);
}
