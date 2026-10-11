/*******************************************************************************
 *
 *              #####   File : eScopeCfg.h
 *              #####
 *              #####   eScope project configuration — FPB-RA4E1
 *              #####
 *      #############   Protocol  : V3ID (full command set, embedded config buffer)
 *   ################   Transport : SEGGER RTT (default) or UART SCI9 115200
 *  ######     ######
 *  ######      #####   Call eScopeTransport_Poll(&sTransport) from your main loop
 *  *##########         BEFORE eScopeProtocol_Process().
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
 * Protocol version
 ******************************************************************************/

#define ESCOPE_PROTOCOL_V3ID

/*******************************************************************************
 * Transport — uncomment exactly one
 ******************************************************************************/

   #define ESCOPE_TRANSPORT_RTT     /* SEGGER RTT through the J-Link OB (SWD)          */
/* #define ESCOPE_TRANSPORT_UART */ /* SCI9 P109/P110, needs the g_uart9 FSP stack     */

/* UART transport only */
#define ESCOPE_PROTOCOL_BAUDRATE 115200

/*******************************************************************************
 * Protocol parameters
 ******************************************************************************/

#define ESCOPE_PROTOCOL_CHAN_MAX      (8U)
#define ESCOPE_PROTOCOL_ADDRESS_SIZE  (4U)
#define ESCOPE_PROTOCOL_DATASIZE_SIZE (4U)
#define ESCOPE_PROTOCOL_TX_SIZE       (128U)

/*******************************************************************************
 * Sampling period (ms) = period of eScopeProtocol_Process() in hal_entry.c.
 * Enter the same value as "Sampling Time" in the eScope device.
 * At 115200 baud a UART carries ~11 bytes/ms: 5 ms leaves room for 8 floats.
 ******************************************************************************/

#ifdef ESCOPE_TRANSPORT_RTT
   #define ESCOPE_SAMPLING_MS  (1U)
#else
   #define ESCOPE_SAMPLING_MS  (5U)
#endif

#endif /* ESCOPE_CFG_H_ */
