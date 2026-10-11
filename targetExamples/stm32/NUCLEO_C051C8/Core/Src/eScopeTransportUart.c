/*******************************************************************************
 *
 *              #####   File : eScopeTransportUart.c
 *              #####
 *              #####   UART transport — STM32 HAL implementation (NUCLEO-C051C8)
 *              #####
 *      #############   Compiled when ESCOPE_TRANSPORT_UART is defined in eScopeCfg.h.
 *   ################
 *  ######     ######   u8Id selects the UART (see eScopeCfg.h):
 *  ######      #####     ESCOPE_UART_VCP    USART2 PA2/PA3, ST-LINK virtual COM port
 *  *##########           ESCOPE_UART_BRIDGE USART1 PB6/PB7, external bridge
 *    ##############                         (Bluetooth / Wi-Fi ESP32, USB-UART …)
 *         ###########
 *    ##         ######  RX model : interrupt-driven, one byte at a time.
 *  ######        #####              HAL_UART_Receive_IT re-armed on each byte.
 *  ######       ######              HAL_UART_RxCpltCallback() must call
 *                                   eScopeTransportUartStm32_RxCallback().
 *                       TX model : HAL_UART_Transmit (blocking).
 *
 ******************************************************************************/

#include "main.h"
#include "eScopeTransport.h"
#ifdef ESCOPE_TRANSPORT_UART

extern UART_HandleTypeDef huart1;   /* main.c, initialised when the bridge UART is used */

static UART_HandleTypeDef* eScopeTransportUart_Handle(const eScopeTransport_t* pThis)
{
    return (pThis->u8Id == ESCOPE_UART_BRIDGE) ? &huart1 : &hcom_uart[COM1];
}

/*******************************************************************************
 * eScopeTransport common API
 ******************************************************************************/

void eScopeTransport_Init(eScopeTransport_t*          pThis,
                          const eScopeTransportCfg_t* pCfg,
                          void (*pfCB)(uint32_t, void*),
                          void* pvCtxt)
{
    pThis->u8Id   = pCfg->u8Id;
    pThis->pfCB   = pfCB;
    pThis->pvCtxt = pvCtxt;

    /* the UART is already initialised (BSP_COM_Init or MX_USART1_UART_Init),
       only its interrupt is enabled here */
    IRQn_Type irq = (pThis->u8Id == ESCOPE_UART_BRIDGE) ? USART1_IRQn : USART2_IRQn;
    HAL_NVIC_SetPriority(irq, 1U, 0U);
    HAL_NVIC_EnableIRQ(irq);
    /* First HAL_UART_Receive_IT is armed by the protocol via eScopeTransport_Read */
}

void eScopeTransport_DeInit(eScopeTransport_t* pThis)
{
    HAL_UART_Abort(eScopeTransportUart_Handle(pThis));
}

void eScopeTransport_Read(eScopeTransport_t* pThis, uint8_t* pBuf, uint8_t u8Len)
{
    /* Arm next interrupt-driven receive directly into the protocol buffer */
    HAL_UART_Receive_IT(eScopeTransportUart_Handle(pThis), pBuf, u8Len);
}

void eScopeTransport_Write(eScopeTransport_t* pThis, const uint8_t* pData, uint16_t u16Len)
{
    HAL_UART_Transmit(eScopeTransportUart_Handle(pThis), (uint8_t*)pData, u16Len, HAL_MAX_DELAY);
}

void eScopeTransportUart_Poll(eScopeTransportUart_t* pThis)
{
    (void)pThis;   /* interrupt-driven: nothing to poll */
}

/*******************************************************************************
 * STM32-specific helpers
 ******************************************************************************/

/* call from HAL_UART_RxCpltCallback() (main.c); the USART IRQ handlers are in stm32c0xx_it.c */
void eScopeTransportUartStm32_RxCallback(eScopeTransport_t* pThis, UART_HandleTypeDef* huart)
{
    if ((huart == eScopeTransportUart_Handle(pThis)) && (pThis->pfCB != NULL))
        pThis->pfCB(ESCOPE_TRANSPORT_EVT_RX, pThis->pvCtxt);
}

#endif /* ESCOPE_TRANSPORT_UART */
