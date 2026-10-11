/*******************************************************************************
 *
 *              #####   File : eScopeCfg.h
 *              #####
 *              #####   Project configuration — copy this file into your
 *              #####   project and uncomment the desired options.
 *      #############
 *   ################   This file MUST be on your compiler include path.
 *  ######     ######   It is included by eScopeProtocol.h and
 *  ######      #####   eScopeTransport.h to select the concrete
 *  *##########         implementations at compile time.
 *    ##############
 *         ###########
 *    ##         ######
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#ifndef ESCOPE_CFG_H_
#define ESCOPE_CFG_H_

/*******************************************************************************
 * Protocol version — uncomment exactly one
 ******************************************************************************/

/* #define ESCOPE_PROTOCOL_V1   */  /* fixed channel count, fixed data size    */
/* #define ESCOPE_PROTOCOL_V2   */  /* per-channel address + data size         */
   #define ESCOPE_PROTOCOL_V3       /* full command set (read / write / clear) */
/* #define ESCOPE_PROTOCOL_V3ID */  /* V3 for ID-based UART driver             */

/*******************************************************************************
 * Transport — uncomment exactly one
 ******************************************************************************/

   #define ESCOPE_TRANSPORT_UART    /* UART: ST-LINK virtual COM port or bridge */
/* #define ESCOPE_TRANSPORT_RTT  */ /* SEGGER RTT through the ST-LINK (SWD)     */

/* UART transport only */
   #define ESCOPE_PROTOCOL_BAUDRATE 115200

   #define ESCOPE_UART_VCP     (0U) /* USART2 PA2/PA3: ST-LINK virtual COM port (USB cable) */
   #define ESCOPE_UART_BRIDGE  (1U) /* USART1 PB6/PB7: external bridge (Bluetooth, Wi-Fi…)  */
   #define ESCOPE_UART_ID      ESCOPE_UART_VCP

/*******************************************************************************
 * Protocol parameters
 ******************************************************************************/

/* Common to all versions */
   #define ESCOPE_PROTOCOL_CHAN_MAX      (8U)   /* number of channels           */
   #define ESCOPE_PROTOCOL_ADDRESS_SIZE  (4U)   /* address width in bytes       */

/* V1 only */
/* #define ESCOPE_PROTOCOL_DATA_TYPE  uint16_t */  /* fixed per-channel type   */

/* V2 / V3 / V3ID */
   #define ESCOPE_PROTOCOL_DATASIZE_SIZE (4U)   /* data-size field width        */
   #define ESCOPE_PROTOCOL_TX_SIZE       (128U) /* TX buffer size in bytes      */

/*******************************************************************************
 * Sampling period (ms) = period of eScopeProtocol_Process() in main.c.
 * Enter the same value as "Sampling Time" in the eScope device.
 * At 115200 baud a UART carries ~11 bytes/ms: 5 ms leaves room for 8 floats.
 ******************************************************************************/

#ifdef ESCOPE_TRANSPORT_RTT
   #define ESCOPE_SAMPLING_MS  (1U)
#else
   #define ESCOPE_SAMPLING_MS  (5U)
#endif

#endif /* ESCOPE_CFG_H_ */
