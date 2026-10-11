/*******************************************************************************
 *
 *              #####   File : eScopeProtocolV2.c
 *              #####
 *              #####   Function : debug
 *              #####
 *      #############    Abstract — ProtocolV2
 *   ################   --------------------------------------------------------
 *  ######     ######
 *  ######      #####
 *  *##########
 *    ##############
 *         ###########
 *    ##         ######
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#include "eScopeProtocol.h"
#ifdef ESCOPE_PROTOCOL_V2

#include <string.h>

static uint8_t eScopeProtocol_sample(uint8_t** pCfg, uint8_t** pOut);
static void    eScopeProtocol_transport_cb(uint32_t u32Event, void* pvCtxt);

/*----------------------------------------------------------------------------*/
void eScopeProtocol_Init(eScopeProtocol_t*          pThis,
                         const eScopeTransportCfg_t* pCfg,
                         eScopeProtocolCfg_t*        psCfg)
{
    memset(pThis, 0, sizeof(eScopeProtocol_t));
    pThis->pu8EepromCfg = psCfg->pu8Cfg;
    eScopeTransport_Init(&pThis->sTransport, pCfg,
                         eScopeProtocol_transport_cb, pThis);
    eScopeTransport_Read(&pThis->sTransport, &pThis->u8RxByte, 1U);
}

/*----------------------------------------------------------------------------*/
void eScopeProtocol_DeInit(eScopeProtocol_t* pThis)
{
    eScopeTransport_DeInit(&pThis->sTransport);
    memset(pThis, 0, sizeof(eScopeProtocol_t));
}

/*----------------------------------------------------------------------------*/
void eScopeProtocol_Process(eScopeProtocol_t* pThis)
{
    uint8_t* pu8Cfg;
    uint8_t* pu8Tx;

    if (!pThis->pu8EepromCfg) return;

    if ((pThis->u8RxIdx != 0U) && (pThis->u8Time != 0U))
    {
        if (*(uint16_t*)&pThis->pu8EepromCfg[0] == 0xFFFEU)
        {
            pThis->pu8EepromCfg = NULL;
            return;
        }
        uint8_t* pReset = &pThis->pu8EepromCfg[pThis->u8RxIdx];
        uint8_t* pEnd   = &pThis->pu8EepromCfg[ESCOPE_PROTOCOL_EEPROM_SIZE];
        while (pReset < pEnd) *pReset++ = 0U;
        pThis->u8RxIdx = 0U;
    }

    if (pThis->u8RxIdx == 0U)
    {
        pu8Cfg = &pThis->pu8EepromCfg[0];
        pu8Tx  = (uint8_t*)&pThis->tu8Tx[4];

        if (pu8Cfg[4])
        {
            uint8_t  u8ChanIdx = 0U;
            uint16_t u16Size   = 4U;

            pThis->tu8Tx[0] = 0x55U;
            pThis->tu8Tx[1] = 0xAAU;
            pThis->tu8Tx[2] = 0x33U;
            pThis->tu8Tx[3] = pThis->u8Time;

            while (pu8Cfg[4] && (u8ChanIdx < ESCOPE_PROTOCOL_CHAN_MAX))
            {
                u16Size = (uint16_t)(u16Size + eScopeProtocol_sample(&pu8Cfg, &pu8Tx));
                u8ChanIdx++;
            }
            eScopeTransport_Write(&pThis->sTransport, pThis->tu8Tx, u16Size);
        }
    }
    pThis->u8Time++;
}

/*----------------------------------------------------------------------------*/
bool eScopeProtocol_isEnd(eScopeProtocol_t* pThis)
{
    return (pThis->pu8EepromCfg == NULL);
}

/*----------------------------------------------------------------------------*/
static uint8_t eScopeProtocol_sample(uint8_t** pCfg, uint8_t** pOut)
{
    uint8_t  u8Size        = (*pCfg)[4];
    uint8_t* pu8Address    = (uint8_t*)*((uint32_t*)*pCfg);
    uint8_t* pu8AddressEnd = pu8Address + u8Size;
    uint8_t* pu8Tx         = *pOut;

    while (pu8Address < pu8AddressEnd) *pu8Tx++ = *pu8Address++;
    *pOut = *pOut + u8Size;
    *pCfg = *pCfg + 8;
    return u8Size;
}

/*----------------------------------------------------------------------------*/
static void eScopeProtocol_transport_cb(uint32_t u32Event, void* pvCtxt)
{
    eScopeProtocol_t* pThis = (eScopeProtocol_t*)pvCtxt;
    CRITICAL_SECTION_ALLOC();

    if (u32Event != ESCOPE_TRANSPORT_EVT_RX) return;

    CRITICAL_SECTION_ENTER();
    if (pThis->u8RxIdx < ESCOPE_PROTOCOL_EEPROM_SIZE)
    {
        pThis->pu8EepromCfg[pThis->u8RxIdx] = pThis->u8RxByte;
        pThis->u8RxIdx++;
    }
    pThis->u8Time = 0U;
    CRITICAL_SECTION_EXIT();
    eScopeTransport_Read(&pThis->sTransport, &pThis->u8RxByte, 1U);
}

#endif /* ESCOPE_PROTOCOL_V2 */
