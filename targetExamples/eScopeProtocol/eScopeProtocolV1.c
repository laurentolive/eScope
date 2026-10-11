/*******************************************************************************
 *
 *              #####   File : eScopeProtocolV1.c
 *              #####
 *              #####   Function : debug
 *              #####
 *      #############    Abstract
 *   ################   --------------------------------------------------------
 *  ######     ######
 *  ######      #####    Scope MCU internal memory — ProtocolV1
 *  *##########
 *    ##############     PC → Target : CHAN_MAX × ADDRESS_SIZE bytes (addresses, little-endian)
 *         ###########   Target → PC : [0x55][flowId][data...][0xAA]
 *    ##         ######
 *  ######        #####  CHAN_MAX and DATA_SIZE are build-time constants.
 *  ######       ######  Adjust ESCOPE_PROTOCOL_CHAN_MAX / DATA_TYPE in eScopeCfg.h
 *
 ******************************************************************************/

#include "eScopeProtocol.h"
#ifdef ESCOPE_PROTOCOL_V1

#include <string.h>

#define HEADER_SYNC_BYTE  (0x55U)
#define FOOTER_SYNC_BYTE  (0xAAU)
#define HEADER_SIZE       (4U)
#define FOOTER_SIZE       (1U)
#define CMD_READ_PERIODIC (0x43U)

static void eScopeProtocol_transport_cb(uint32_t u32Event, void* pvCtxt);

/*----------------------------------------------------------------------------*/
bool eScopeProtocol_isEnd(eScopeProtocol_t* pThis)
{
    (void)pThis;
    return false; /* V1 has no CMD_DEINIT */
}

/*----------------------------------------------------------------------------*/
void eScopeProtocol_Init(eScopeProtocol_t*          pThis,
                         const eScopeTransportCfg_t* pCfg,
                         eScopeProtocolCfg_t*        psCfg)
{
    memset(pThis, 0, sizeof(eScopeProtocol_t));
    pThis->psCfg = psCfg;
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
    CRITICAL_SECTION_ALLOC();

    if ((pThis->u8RxIdx != 0U) && (pThis->u8Time != 0U))
    {
        if (pThis->u8RxIdx == ESCOPE_PROTOCOL_CFG_SIZE)
            pThis->bConfigured = true;
        CRITICAL_SECTION_ENTER();
        pThis->u8RxIdx = 0U;
        CRITICAL_SECTION_EXIT();
    }

    CRITICAL_SECTION_ENTER();
    if (pThis->bConfigured)
    {
        pThis->tu8Tx[0] = HEADER_SYNC_BYTE;
        pThis->tu8Tx[1] = pThis->u8FrameCnt++;
        *(ESCOPE_PROTOCOL_DATA_TYPE*)&pThis->tu8Tx[2+0*ESCOPE_PROTOCOL_DATA_SIZE] = *((ESCOPE_PROTOCOL_DATA_TYPE*)pThis->psCfg->tu8Address[0*ESCOPE_PROTOCOL_ADDRESS_SIZE]);
        *(ESCOPE_PROTOCOL_DATA_TYPE*)&pThis->tu8Tx[2+1*ESCOPE_PROTOCOL_DATA_SIZE] = *((ESCOPE_PROTOCOL_DATA_TYPE*)pThis->psCfg->tu8Address[1*ESCOPE_PROTOCOL_ADDRESS_SIZE]);
        *(ESCOPE_PROTOCOL_DATA_TYPE*)&pThis->tu8Tx[2+2*ESCOPE_PROTOCOL_DATA_SIZE] = *((ESCOPE_PROTOCOL_DATA_TYPE*)pThis->psCfg->tu8Address[2*ESCOPE_PROTOCOL_ADDRESS_SIZE]);
        *(ESCOPE_PROTOCOL_DATA_TYPE*)&pThis->tu8Tx[2+3*ESCOPE_PROTOCOL_DATA_SIZE] = *((ESCOPE_PROTOCOL_DATA_TYPE*)pThis->psCfg->tu8Address[3*ESCOPE_PROTOCOL_ADDRESS_SIZE]);
        CRITICAL_SECTION_EXIT();
        pThis->tu8Tx[2+4*ESCOPE_PROTOCOL_DATA_SIZE] = FOOTER_SYNC_BYTE;
        eScopeTransport_Write(&pThis->sTransport, pThis->tu8Tx, ESCOPE_PROTOCOL_TX_SIZE);
    }
    else
    {
        CRITICAL_SECTION_EXIT();
    }

    CRITICAL_SECTION_ENTER();
    pThis->u8Time++;
    CRITICAL_SECTION_EXIT();
}

/*----------------------------------------------------------------------------*/
static void eScopeProtocol_transport_cb(uint32_t u32Event, void* pvCtxt)
{
    eScopeProtocol_t* pThis = (eScopeProtocol_t*)pvCtxt;
    CRITICAL_SECTION_ALLOC();

    if (u32Event != ESCOPE_TRANSPORT_EVT_RX) return;

    CRITICAL_SECTION_ENTER();
    if (pThis->u8RxIdx < ESCOPE_PROTOCOL_CFG_SIZE)
    {
        pThis->bConfigured = false;
        pThis->psCfg->tu8Address[pThis->u8RxIdx] = pThis->u8RxByte;
        pThis->u8RxIdx++;
    }
    pThis->u8Time = 0U;
    CRITICAL_SECTION_EXIT();
    eScopeTransport_Read(&pThis->sTransport, &pThis->u8RxByte, 1U);
}

#endif /* ESCOPE_PROTOCOL_V1 */
